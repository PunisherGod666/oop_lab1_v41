using Windows.Storage.Pickers;
using WinRT.Interop;

namespace SpreadsheetApp.UI;

/// <summary>Windows Explorer file dialogs for an unpackaged WinUI app.</summary>
public sealed class WindowsFilePickerService : IFilePickerService
{
    // Windows remembers the last folder per identifier
    private const string SettingsIdentifier = "SpreadsheetApp";

    public async Task<string?> PickSaveFileAsync(string suggestedName, string fileTypeDescription, string extension)
    {
        var picker = new FileSavePicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            SuggestedFileName = suggestedName,
            SettingsIdentifier = SettingsIdentifier,
        };
        picker.FileTypeChoices.Add(fileTypeDescription, new List<string> { extension });
        InitializeWithWindow.Initialize(picker, GetWindowHandle());

        var file = await picker.PickSaveFileAsync();
        return file?.Path;
    }

    public async Task<string?> PickOpenFileAsync(string extension)
    {
        var picker = new FileOpenPicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            ViewMode = PickerViewMode.List,
            SettingsIdentifier = SettingsIdentifier,
        };
        picker.FileTypeFilter.Add(extension);
        InitializeWithWindow.Initialize(picker, GetWindowHandle());

        var file = await picker.PickSingleFileAsync();
        return file?.Path;
    }

    // Pickers must be bound to the app window handle
    private static IntPtr GetWindowHandle()
    {
        var window = Microsoft.Maui.Controls.Application.Current?.Windows.FirstOrDefault()?.Handler?.PlatformView
            as Microsoft.UI.Xaml.Window
            ?? throw new InvalidOperationException("Немає активного вікна для показу діалогу.");
        return WindowNative.GetWindowHandle(window);
    }
}
