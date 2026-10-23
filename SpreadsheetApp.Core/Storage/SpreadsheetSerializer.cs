using System.Text.Json;
using SpreadsheetApp.Model;

namespace SpreadsheetApp.Storage;

/// <summary>Converts a spreadsheet to JSON and back; only size and expressions are stored.</summary>
public sealed class SpreadsheetSerializer
{
    public const string FileExtension = ".json";
    private const int FormatVersion = 1;

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public byte[] Serialize(Spreadsheet sheet)
    {
        var document = new SpreadsheetDocument
        {
            Version = FormatVersion,
            Rows = sheet.RowCount,
            Columns = sheet.ColumnCount,
            Cells = sheet.Cells
                .OrderBy(c => c.Address.Row).ThenBy(c => c.Address.Column)
                .Select(c => new CellDocument { Address = c.Address.ToString(), Expression = c.Expression })
                .ToList(),
        };
        return JsonSerializer.SerializeToUtf8Bytes(document, Options);
    }

    // Values are recalculated while expressions are loaded
    public Spreadsheet Deserialize(byte[] data)
    {
        SpreadsheetDocument? document;
        try
        {
            document = JsonSerializer.Deserialize<SpreadsheetDocument>(data, Options);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("Файл пошкоджений або має невідомий формат.", ex);
        }

        if (document is null || document.Rows < 1 || document.Columns < 1)
            throw new InvalidDataException("Файл не містить коректної таблиці.");
        if (document.Version > FormatVersion)
            throw new InvalidDataException($"Файл створено новішою версією програми (формат {document.Version}).");

        var sheet = new Spreadsheet(document.Rows, document.Columns);
        foreach (var cell in document.Cells ?? new List<CellDocument>())
        {
            if (!CellAddress.TryParse(cell.Address, out var address) || !sheet.Contains(address))
                throw new InvalidDataException($"Некоректна адреса клітинки у файлі: «{cell.Address}».");
            sheet.SetExpression(address, cell.Expression ?? string.Empty);
        }
        return sheet;
    }

    // JSON file layout
    private sealed class SpreadsheetDocument
    {
        public int Version { get; set; }
        public int Rows { get; set; }
        public int Columns { get; set; }
        public List<CellDocument>? Cells { get; set; }
    }

    private sealed class CellDocument
    {
        public string? Address { get; set; }
        public string? Expression { get; set; }
    }
}
