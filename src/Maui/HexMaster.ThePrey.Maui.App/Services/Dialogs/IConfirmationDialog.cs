namespace HexMaster.ThePrey.Maui.App.Services.Dialogs;

/// <summary>
/// A testable confirm/cancel dialog seam. Keeps view models free of MAUI UI types (they depend on
/// this interface, not <c>Page.DisplayAlert</c>) so they stay linkable and mockable in the plain
/// <c>net10.0</c> test project. The MAUI alert lives in the implementation.
/// </summary>
public interface IConfirmationDialog
{
    /// <summary>
    /// Shows a modal confirmation with the given title/message and accept/cancel button captions.
    /// Returns <c>true</c> only when the user chooses accept; <c>false</c> on cancel or dismissal.
    /// </summary>
    Task<bool> ConfirmAsync(string title, string message, string accept, string cancel);
}
