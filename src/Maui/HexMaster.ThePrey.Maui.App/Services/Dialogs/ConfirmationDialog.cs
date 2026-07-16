namespace HexMaster.ThePrey.Maui.App.Services.Dialogs;

/// <summary>
/// Default <see cref="IConfirmationDialog"/> over <c>Page.DisplayAlert</c>. Resolves the current page
/// (the active <see cref="Shell"/>, falling back to the first window's page) and shows the alert,
/// marshalling to the main thread so the view model can call it from any context. Returns <c>false</c>
/// when no page is available rather than throwing.
/// </summary>
public sealed class ConfirmationDialog : IConfirmationDialog
{
    public Task<bool> ConfirmAsync(string title, string message, string accept, string cancel) =>
        MainThread.InvokeOnMainThreadAsync(() =>
        {
            var page = Shell.Current ?? Application.Current?.Windows.FirstOrDefault()?.Page;
            return page is null
                ? Task.FromResult(false)
                : page.DisplayAlertAsync(title, message, accept, cancel);
        });
}
