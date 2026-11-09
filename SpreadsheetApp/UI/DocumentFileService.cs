using SpreadsheetApp.Model;
using SpreadsheetApp.Storage;

namespace SpreadsheetApp.UI;

/// <summary>A spreadsheet loaded from a file.</summary>
public sealed record OpenedDocument(Spreadsheet Sheet, string FileName);

/// <summary>Save and open workflows (dialogs + format + storage) that work with any IFileStorage.</summary>
public interface IDocumentFileService
{
    // Returns the saved file name, or null if cancelled
    Task<string?> SaveAsAsync(Spreadsheet sheet, IFileStorage storage, string currentFileName);

    Task SaveAsync(Spreadsheet sheet, IFileStorage storage, string fileName);

    // Returns null if cancelled
    Task<OpenedDocument?> OpenAsync(IFileStorage storage);
}

public sealed class DocumentFileService : IDocumentFileService
{
    private const string DefaultFileName = "Таблиця";

    private readonly ISpreadsheetSerializer _serializer;
    private readonly IDialogService _dialogs;
    private readonly IFilePickerService _filePicker;

    public DocumentFileService(ISpreadsheetSerializer serializer, IDialogService dialogs, IFilePickerService filePicker)
    {
        _serializer = serializer;
        _dialogs = dialogs;
        _filePicker = filePicker;
    }

    public async Task<string?> SaveAsAsync(Spreadsheet sheet, IFileStorage storage, string currentFileName)
    {
        string suggested = currentFileName.Length > 0
            ? Path.GetFileNameWithoutExtension(currentFileName)
            : DefaultFileName;

        string? fileName = storage.UsesSystemFileDialog
            ? await _filePicker.PickSaveFileAsync(suggested, _serializer.FileTypeDescription, _serializer.FileExtension)
            : await AskFileNameAsync(storage, suggested, currentFileName);
        if (fileName is null)
            return null;

        await SaveAsync(sheet, storage, fileName);
        return fileName;
    }

    public Task SaveAsync(Spreadsheet sheet, IFileStorage storage, string fileName) =>
        storage.SaveAsync(fileName, _serializer.Serialize(sheet));

    public async Task<OpenedDocument?> OpenAsync(IFileStorage storage)
    {
        string? fileName = storage.UsesSystemFileDialog
            ? await _filePicker.PickOpenFileAsync(_serializer.FileExtension)
            : await ChooseFileAsync(storage);
        if (fileName is null)
            return null;

        byte[] data = await storage.LoadAsync(fileName);
        return new OpenedDocument(_serializer.Deserialize(data), fileName);
    }

    // Name prompt for storages without a system dialog; confirms overwriting
    private async Task<string?> AskFileNameAsync(IFileStorage storage, string suggested, string currentFileName)
    {
        string? input = await _dialogs.PromptAsync("Зберегти таблицю",
            $"Ім'я файлу (сховище: {storage.DisplayName}):", suggested);
        if (string.IsNullOrWhiteSpace(input))
            return null;

        string fileName = NormalizeFileName(input);
        var existing = await storage.ListFilesAsync();
        if (existing.Contains(fileName, StringComparer.OrdinalIgnoreCase) &&
            !fileName.Equals(currentFileName, StringComparison.OrdinalIgnoreCase) &&
            !await _dialogs.ConfirmAsync("Файл уже існує", $"Файл «{fileName}» уже існує. Замінити його?"))
            return null;

        return fileName;
    }

    private async Task<string?> ChooseFileAsync(IFileStorage storage)
    {
        var files = await storage.ListFilesAsync();
        if (files.Count == 0)
        {
            await _dialogs.AlertAsync("Відкрити таблицю", $"У сховищі «{storage.DisplayName}» ще немає збережених таблиць.");
            return null;
        }
        return await _dialogs.ChooseAsync($"Відкрити таблицю ({storage.DisplayName})", files);
    }

    private string NormalizeFileName(string input)
    {
        string name = input.Trim();
        foreach (char c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        if (!name.EndsWith(_serializer.FileExtension, StringComparison.OrdinalIgnoreCase))
            name += _serializer.FileExtension;
        return name;
    }
}
