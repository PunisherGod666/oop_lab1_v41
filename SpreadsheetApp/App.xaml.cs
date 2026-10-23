using SpreadsheetApp.UI;

namespace SpreadsheetApp;

public partial class App : Application
{
    private readonly IServiceProvider _services;

    public App(IServiceProvider services)
    {
        InitializeComponent();
        _services = services;
        // The theme has its own colors, so ignore the system dark mode
        UserAppTheme = AppTheme.Light;
    }

    // Main window; the page is resolved here, after app resources are loaded
    protected override Window CreateWindow(IActivationState? activationState)
    {
        var mainPage = _services.GetRequiredService<MainPage>();
        var navigation = new NavigationPage(mainPage)
        {
            BarBackgroundColor = Color.FromArgb("#1C2541"),
            BarTextColor = Color.FromArgb("#F4B942"),
        };
        return new Window(navigation)
        {
            Title = "Електронні таблиці",
            Width = 1280,
            Height = 800,
            MinimumWidth = 700,
            MinimumHeight = 450,
        };
    }
}
