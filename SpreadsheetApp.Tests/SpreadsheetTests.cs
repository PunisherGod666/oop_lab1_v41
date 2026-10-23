using SpreadsheetApp.Model;
using SpreadsheetApp.Parsing;
using SpreadsheetApp.Storage;

namespace SpreadsheetApp.Tests;

public class SpreadsheetTests
{
    private static CellAddress A(string text) => CellAddress.Parse(text);

    [Theory]
    [InlineData("A1", 0, 0)]
    [InlineData("Z10", 9, 25)]
    [InlineData("AA1", 0, 26)]
    [InlineData("ab3", 2, 27)]
    public void CellAddressParsing(string text, int row, int column)
    {
        var address = A(text);
        Assert.Equal(row, address.Row);
        Assert.Equal(column, address.Column);
        Assert.Equal(text.ToUpperInvariant(), address.ToString());
    }

    [Fact]
    public void DependentCellsAreRecalculated()
    {
        var sheet = new Spreadsheet();
        sheet.SetExpression(A("A1"), "5");
        sheet.SetExpression(A("B1"), "A1 * 2");
        sheet.SetExpression(A("C1"), "B1 + A1");
        Assert.Equal("15", sheet.GetCell(A("C1")).Value.ToString());

        sheet.SetExpression(A("A1"), "10");
        Assert.Equal("20", sheet.GetCell(A("B1")).Value.ToString());
        Assert.Equal("30", sheet.GetCell(A("C1")).Value.ToString());
    }

    [Fact]
    public void EmptyCellIsZero()
    {
        var sheet = new Spreadsheet();
        sheet.SetExpression(A("A1"), "B1 + 1");
        Assert.Equal("1", sheet.GetCell(A("A1")).Value.ToString());
    }

    [Fact]
    public void CyclesAreDetectedAndResolvedWhenBroken()
    {
        var sheet = new Spreadsheet();
        sheet.SetExpression(A("A1"), "B1 + 1");
        sheet.SetExpression(A("B1"), "C1 + 1");
        sheet.SetExpression(A("C1"), "A1 + 1");

        Assert.True(sheet.HasCycle(A("A1")));
        foreach (var name in new[] { "A1", "B1", "C1" })
            Assert.Equal(ErrorKind.CyclicReference, sheet.GetCell(A(name)).Error?.Kind);

        sheet.SetExpression(A("C1"), "1");
        Assert.False(sheet.HasCycle(A("A1")));
        Assert.Equal("3", sheet.GetCell(A("A1")).Value.ToString());
    }

    [Fact]
    public void SelfReferenceIsCycle()
    {
        var sheet = new Spreadsheet();
        sheet.SetExpression(A("A1"), "A1 + 1");
        Assert.Equal(ErrorKind.CyclicReference, sheet.GetCell(A("A1")).Error?.Kind);
    }

    [Fact]
    public void ErrorsPropagateToDependents()
    {
        var sheet = new Spreadsheet();
        sheet.SetExpression(A("A1"), "1 / 0");
        sheet.SetExpression(A("A2"), "A1 + 1");
        Assert.Equal(ErrorKind.DivisionByZero, sheet.GetCell(A("A2")).Error?.Kind);

        sheet.SetExpression(A("A1"), "4");
        Assert.Equal("5", sheet.GetCell(A("A2")).Value.ToString());
    }

    [Fact]
    public void ReferencesOutsideTheTableAreInvalid()
    {
        var sheet = new Spreadsheet(3, 3);
        sheet.SetExpression(A("A1"), "C3 + 1");
        Assert.True(sheet.GetCell(A("A1")).Error is null);

        sheet.RemoveRow();
        Assert.Equal(ErrorKind.InvalidReference, sheet.GetCell(A("A1")).Error?.Kind);

        sheet.AddRow();
        Assert.Equal("1", sheet.GetCell(A("A1")).Value.ToString());
    }

    [Fact]
    public void SyntaxErrorIsStoredInCell()
    {
        var sheet = new Spreadsheet();
        sheet.SetExpression(A("A1"), "1 + * 2");
        var error = sheet.GetCell(A("A1")).Error;
        Assert.Equal(ErrorKind.Syntax, error?.Kind);
        Assert.Equal(4, error?.Position);
    }

    [Fact]
    public void SerializerRoundTrip()
    {
        var sheet = new Spreadsheet(4, 5);
        sheet.SetExpression(A("A1"), "7");
        sheet.SetExpression(A("E4"), "A1 ^ 2 > 40");

        var serializer = new SpreadsheetSerializer();
        var loaded = serializer.Deserialize(serializer.Serialize(sheet));

        Assert.Equal(4, loaded.RowCount);
        Assert.Equal(5, loaded.ColumnCount);
        Assert.Equal("A1 ^ 2 > 40", loaded.GetCell(A("E4")).Expression);
        Assert.Equal(Value.TrueText, loaded.GetCell(A("E4")).Value.ToString());
    }

    [Fact]
    public async Task LocalStorageSavesAndLists()
    {
        string dir = Path.Combine(Path.GetTempPath(), "SpreadsheetAppTests", Guid.NewGuid().ToString("N"));
        try
        {
            var storage = new LocalFileStorage(dir);
            await storage.SaveAsync("test.json", new byte[] { 1, 2, 3 });
            Assert.Equal(new[] { "test.json" }, await storage.ListFilesAsync());
            Assert.Equal(new byte[] { 1, 2, 3 }, await storage.LoadAsync("test.json"));
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task LocalStorageAcceptsFullPathChosenInExplorer()
    {
        string root = Path.Combine(Path.GetTempPath(), "SpreadsheetAppTests", Guid.NewGuid().ToString("N"));
        string chosen = Path.Combine(root, "Інша тека", "Моя таблиця.json");
        try
        {
            var storage = new LocalFileStorage(Path.Combine(root, "default"));
            await storage.SaveAsync(chosen, new byte[] { 7 });
            Assert.True(File.Exists(chosen));
            Assert.Equal(new byte[] { 7 }, await storage.LoadAsync(chosen));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task GoogleClientSecretsImportRejectsWebClient()
    {
        string root = Path.Combine(Path.GetTempPath(), "SpreadsheetAppTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string web = Path.Combine(root, "web.json");
            await File.WriteAllTextAsync(web, """{"web":{"client_id":"x","client_secret":"y"}}""");
            string desktop = Path.Combine(root, "desktop.json");
            await File.WriteAllTextAsync(desktop, """{"installed":{"client_id":"x","client_secret":"y"}}""");

            var storage = new GoogleDriveStorage(Path.Combine(root, "app", "client_secret.json"), root);
            await Assert.ThrowsAsync<InvalidDataException>(() => storage.ImportClientSecretsAsync(web));
            Assert.False(storage.HasClientSecrets);

            await storage.ImportClientSecretsAsync(desktop);
            Assert.True(storage.HasClientSecrets);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
