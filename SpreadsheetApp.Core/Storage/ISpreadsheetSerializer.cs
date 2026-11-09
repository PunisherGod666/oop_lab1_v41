using SpreadsheetApp.Model;

namespace SpreadsheetApp.Storage;

/// <summary>File format of a spreadsheet; storages only move the resulting bytes.</summary>
public interface ISpreadsheetSerializer
{
    string FileExtension { get; }
    string FileTypeDescription { get; }

    byte[] Serialize(Spreadsheet sheet);
    Spreadsheet Deserialize(byte[] data);
}
