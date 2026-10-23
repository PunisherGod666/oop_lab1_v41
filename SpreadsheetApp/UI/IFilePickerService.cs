namespace SpreadsheetApp.UI;

/// <summary>System open/save file dialogs; return a full path or null if cancelled.</summary>
public interface IFilePickerService
{
    Task<string?> PickSaveFileAsync(string suggestedName, string fileTypeDescription, string extension);

    Task<string?> PickOpenFileAsync(string extension);
}
