namespace SpreadsheetApp.UI;

/// <summary>User dialogs, kept behind an interface so the view model does not depend on pages.</summary>
public interface IDialogService
{
    Task AlertAsync(string title, string message);
    Task<bool> ConfirmAsync(string title, string message, string accept = "Так", string cancel = "Ні");
    Task<string?> PromptAsync(string title, string message, string initialValue = "");

    // Returns the chosen option or null if cancelled
    Task<string?> ChooseAsync(string title, IReadOnlyList<string> options);
}

/// <summary>Dialogs shown on the current MAUI page.</summary>
public sealed class MauiDialogService : IDialogService
{
    private const string CancelText = "Скасувати";

    private static Page CurrentPage =>
        Application.Current?.Windows.FirstOrDefault()?.Page
        ?? throw new InvalidOperationException("Немає активного вікна для показу діалогу.");

    public Task AlertAsync(string title, string message) =>
        CurrentPage.DisplayAlertAsync(title, message, "Гаразд");

    public Task<bool> ConfirmAsync(string title, string message, string accept = "Так", string cancel = "Ні") =>
        CurrentPage.DisplayAlertAsync(title, message, accept, cancel);

    public Task<string?> PromptAsync(string title, string message, string initialValue = "") =>
        CurrentPage.DisplayPromptAsync(title, message, "Гаразд", CancelText, initialValue: initialValue)!;

    public async Task<string?> ChooseAsync(string title, IReadOnlyList<string> options)
    {
        string? choice = await CurrentPage.DisplayActionSheetAsync(title, CancelText, null, options.ToArray());
        return choice is null || choice == CancelText ? null : choice;
    }
}
