namespace SpreadsheetApp.Storage;

/// <summary>Place where spreadsheet files are saved and loaded.</summary>
public interface IFileStorage
{
    string DisplayName { get; }

    Task SaveAsync(string fileName, byte[] data);
    Task<byte[]> LoadAsync(string fileName);
    Task<IReadOnlyList<string>> ListFilesAsync();
}
