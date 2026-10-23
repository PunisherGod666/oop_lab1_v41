namespace SpreadsheetApp.UI;

/// <summary>About and syntax help page.</summary>
public partial class HelpPage : ContentPage
{
    public HelpPage()
    {
        InitializeComponent();
    }

    private async void OnBackClicked(object? sender, EventArgs e)
    {
        await Navigation.PopAsync();
    }
}
