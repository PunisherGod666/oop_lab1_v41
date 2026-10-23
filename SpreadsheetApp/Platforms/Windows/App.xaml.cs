namespace SpreadsheetApp.WinUI;

/// <summary>WinUI entry point that starts the MAUI app.</summary>
public partial class App : MauiWinUIApplication
{
	public App()
	{
		this.InitializeComponent();
	}

	protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
