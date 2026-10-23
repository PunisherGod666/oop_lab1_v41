namespace SpreadsheetApp.Storage;

/// <summary>Stores spreadsheet files on the local disk.</summary>
public sealed class LocalFileStorage : IFileStorage
{
    private readonly string _directory;

    public LocalFileStorage(string directory)
    {
        _directory = directory;
    }

    public string DisplayName => "Локальний диск";

    public async Task SaveAsync(string fileName, byte[] data)
    {
        string path = GetPath(fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(path, data);
    }

    public async Task<byte[]> LoadAsync(string fileName)
    {
        string path = GetPath(fileName);
        if (!File.Exists(path))
            throw new FileNotFoundException($"Файл «{fileName}» не знайдено.", path);
        return await File.ReadAllBytesAsync(path);
    }

    public Task<IReadOnlyList<string>> ListFilesAsync()
    {
        IReadOnlyList<string> files = Directory.Exists(_directory)
            ? Directory.EnumerateFiles(_directory, "*" + SpreadsheetSerializer.FileExtension)
                .Select(Path.GetFileName)
                .OfType<string>()
                .Order(StringComparer.CurrentCultureIgnoreCase)
                .ToList()
            : Array.Empty<string>();
        return Task.FromResult(files);
    }

    // Full paths (from the file dialog) are used as is; bare names go to the default folder
    private string GetPath(string fileName)
    {
        if (string.IsNullOrWhiteSpace(Path.GetFileName(fileName)))
            throw new ArgumentException("Порожнє ім'я файлу.", nameof(fileName));
        if (Path.IsPathFullyQualified(fileName))
            return fileName;
        return Path.Combine(_directory, Path.GetFileName(fileName));
    }
}
