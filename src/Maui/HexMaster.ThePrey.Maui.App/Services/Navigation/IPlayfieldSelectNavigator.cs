using HexMaster.ThePrey.Maui.App.Services.Api;

namespace HexMaster.ThePrey.Maui.App.Services.Navigation;

/// <summary>
/// Opens the playfield-selection modal over the current page and resolves with the chosen playfield, or
/// <c>null</c> when the player cancels. Mirrors the app's other await-and-return navigator seams so
/// callers stay free of MAUI/Shell types and remain unit-testable.
/// </summary>
public interface IPlayfieldSelectNavigator
{
    Task<PlayFieldSummary?> SelectPlayfieldAsync(CancellationToken ct = default);
}

/// <summary>
/// The result channel the selection modal's view model uses to hand its outcome back to the navigator:
/// the chosen playfield on confirm, or <c>null</c> on cancel / system-back. Separate from
/// <see cref="IPlayfieldSelectNavigator"/> (which callers use) so the view model depends only on the
/// completion boundary, not on how the modal was opened.
/// </summary>
public interface IPlayfieldSelectResultSink
{
    /// <summary>
    /// Completes the pending selection with <paramref name="result"/> and dismisses the modal. Safe to
    /// call more than once (e.g. confirm racing a system-back): only the first call takes effect.
    /// </summary>
    Task CompleteAsync(PlayFieldSummary? result);
}
