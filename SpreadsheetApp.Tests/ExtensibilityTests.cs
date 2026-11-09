using System.Numerics;
using SpreadsheetApp.Model;
using SpreadsheetApp.Parsing;
using SpreadsheetApp.Storage;

namespace SpreadsheetApp.Tests;

// Checks that new functions and storages plug in without changing existing code
public class ExtensibilityTests
{
    private static CellAddress A(string text) => CellAddress.Parse(text);

    [Fact]
    public void CustomFunctionCanBeRegistered()
    {
        var functions = FunctionRegistry.CreateDefault()
            .Register(new FunctionDefinition("abs", 1, 1, a => BigInteger.Abs(a[0])));
        var sheet = new Spreadsheet(parser: new FormulaParser(functions));

        sheet.SetExpression(A("A1"), "-7");
        sheet.SetExpression(A("B1"), "abs(A1) + mmax(1, 2)");

        Assert.Equal("9", sheet.GetCell(A("B1")).Value.ToString());
    }

    [Fact]
    public void UnregisteredFunctionIsSyntaxError()
    {
        var ex = Assert.Throws<CalcException>(() => new FormulaParser().Parse("abs(1)"));
        Assert.Equal(ErrorKind.Syntax, ex.Kind);
    }

    [Fact]
    public async Task AnyStorageWorksWithTheSerializer()
    {
        IFileStorage storage = new InMemoryStorage();
        ISpreadsheetSerializer serializer = new SpreadsheetSerializer();
        var sheet = new Spreadsheet(3, 3);
        sheet.SetExpression(A("A1"), "2 ^ 10");

        await storage.SaveAsync("t.json", serializer.Serialize(sheet));
        var loaded = serializer.Deserialize(await storage.LoadAsync("t.json"));

        Assert.Equal(new[] { "t.json" }, await storage.ListFilesAsync());
        Assert.Equal("1024", loaded.GetCell(A("A1")).Value.ToString());
    }

    [Fact]
    public async Task ConnectReturnsFalseWhenCredentialsAreCancelled()
    {
        string root = Path.Combine(Path.GetTempPath(), "SpreadsheetAppTests", Guid.NewGuid().ToString("N"));
        IConnectableStorage storage = new GoogleDriveStorage(Path.Combine(root, "client_secret.json"), root);

        Assert.False(await storage.ConnectAsync(new CancelledCredentials()));
        Assert.False(storage.IsConnected);
    }

    [Fact]
    public void StoragesDescribeHowFilesAreNamed()
    {
        Assert.True(new LocalFileStorage("x").UsesSystemFileDialog);
        Assert.False(new GoogleDriveStorage("x.json", "x").UsesSystemFileDialog);
    }

    private sealed class InMemoryStorage : IFileStorage
    {
        private readonly Dictionary<string, byte[]> _files = new();

        public string DisplayName => "Пам'ять";
        public bool UsesSystemFileDialog => false;

        public Task SaveAsync(string fileName, byte[] data)
        {
            _files[fileName] = data;
            return Task.CompletedTask;
        }

        public Task<byte[]> LoadAsync(string fileName) => Task.FromResult(_files[fileName]);

        public Task<IReadOnlyList<string>> ListFilesAsync() =>
            Task.FromResult<IReadOnlyList<string>>(_files.Keys.ToList());
    }

    private sealed class CancelledCredentials : ICredentialsProvider
    {
        public Task<string?> RequestCredentialsFileAsync(string storageName) => Task.FromResult<string?>(null);
    }
}
