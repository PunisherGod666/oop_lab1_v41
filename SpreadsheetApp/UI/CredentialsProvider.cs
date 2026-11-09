using SpreadsheetApp.Storage;

namespace SpreadsheetApp.UI;

/// <summary>Explains why a credentials file is needed and lets the user pick it in Explorer.</summary>
public sealed class CredentialsProvider : ICredentialsProvider
{
    private readonly IDialogService _dialogs;
    private readonly IFilePickerService _filePicker;

    public CredentialsProvider(IDialogService dialogs, IFilePickerService filePicker)
    {
        _dialogs = dialogs;
        _filePicker = filePicker;
    }

    public async Task<string?> RequestCredentialsFileAsync(string storageName)
    {
        bool choose = await _dialogs.ConfirmAsync($"Підключення {storageName}",
            $"Для входу в {storageName} програмі потрібен файл OAuth-клієнта (тип «Desktop app»). " +
            "Як його отримати, описано в «Довідці».\n\nВибрати цей JSON-файл зараз?",
            "Вибрати файл", "Скасувати");
        return choose ? await _filePicker.PickOpenFileAsync(".json") : null;
    }
}
