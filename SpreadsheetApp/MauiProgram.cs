using Microsoft.Extensions.Logging;
using Microsoft.Maui.LifecycleEvents;
using SpreadsheetApp.Storage;
using SpreadsheetApp.UI;

namespace SpreadsheetApp;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

		// Default folder for local files
		string dataDirectory = Path.Combine(
			Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "SpreadsheetApp");

		// Google OAuth client file: next to the exe, in the data folder, or imported into app data
		string clientSecrets = new[]
			{
				Path.Combine(AppContext.BaseDirectory, GoogleDriveStorage.ClientSecretsFileName),
				Path.Combine(dataDirectory, GoogleDriveStorage.ClientSecretsFileName),
			}
			.FirstOrDefault(File.Exists)
			?? Path.Combine(FileSystem.AppDataDirectory, GoogleDriveStorage.ClientSecretsFileName);
		string tokenDirectory = Path.Combine(FileSystem.AppDataDirectory, "GoogleDriveToken");

		// Dependency injection
		builder.Services.AddSingleton(new LocalFileStorage(dataDirectory));
		builder.Services.AddSingleton(new GoogleDriveStorage(clientSecrets, tokenDirectory));
		builder.Services.AddSingleton<SpreadsheetSerializer>();
		builder.Services.AddSingleton<IDialogService, MauiDialogService>();
		builder.Services.AddSingleton<IFilePickerService, WindowsFilePickerService>();
		builder.Services.AddSingleton<SpreadsheetViewModel>();
		builder.Services.AddSingleton<MainPage>();

#if WINDOWS
		// Sidebar buttons (ClassId="nav") are left-aligned like menu items
		Microsoft.Maui.Handlers.ButtonHandler.Mapper.AppendToMapping("NavAlignment", (handler, view) =>
		{
			if (view is Button { ClassId: "nav" })
				handler.PlatformView.HorizontalContentAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Left;
		});

		// Amber focus underline instead of the system accent color
		Microsoft.Maui.Handlers.EntryHandler.Mapper.AppendToMapping("AmberFocus", (handler, view) =>
		{
			var amber = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 0xD9, 0x9A, 0x1E));
			handler.PlatformView.Resources["TextControlBorderBrushFocused"] = amber;
			handler.PlatformView.Resources["TextControlSelectionHighlightColor"] = amber;

			// Formula bar has its own border, so the native one is removed
			if (view is Entry { ClassId: "formula" })
			{
				var none = new Microsoft.UI.Xaml.Thickness(0);
				handler.PlatformView.Resources["TextControlBorderThemeThickness"] = none;
				handler.PlatformView.Resources["TextControlBorderThemeThicknessFocused"] = none;
				handler.PlatformView.BorderThickness = none;
			}
		});

		// Ask to save unsaved changes when the window is closed
		builder.ConfigureLifecycleEvents(events =>
		{
			events.AddWindows(windows => windows.OnWindowCreated(window =>
			{
				bool closeConfirmed = false;
				window.AppWindow.Closing += async (_, args) =>
				{
					var viewModel = IPlatformApplication.Current?.Services.GetService<SpreadsheetViewModel>();
					if (closeConfirmed || viewModel is null || !viewModel.IsModified)
						return;

					args.Cancel = true;
					if (await viewModel.ConfirmDiscardChangesAsync())
					{
						closeConfirmed = true;
						window.Close();
					}
				};
			}));
		});
#endif

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
