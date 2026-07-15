using HexMaster.ThePrey.Maui.App.Services.Api;

namespace HexMaster.ThePrey.Maui.App.Services.Navigation;

/// <summary>
/// Navigation seam for the Edit Playfield flow, keeping <c>EditPlayfieldViewModel</c> free of any
/// MAUI/Shell dependency. Mirrors the create seam: it opens the shared area editor and awaits the edited
/// polygon, and closes the edit page back to the list with the updated playfield (or nothing on cancel),
/// which the list uses to update the matching item in place.
/// </summary>
public interface IEditPlayfieldNavigator
{
    /// <summary>
    /// Opens the area editor seeded with <paramref name="current"/> and resolves with the edited polygon,
    /// or <c>null</c> when the editor was cancelled (leaving the caller's polygon unchanged).
    /// </summary>
    Task<IReadOnlyList<GpsCoordinate>?> EditAreaAsync(IReadOnlyList<GpsCoordinate> current);

    /// <summary>
    /// Closes the edit page and returns to the list. A non-null <paramref name="updated"/> is handed to
    /// the list to replace the matching item in place; <c>null</c> just closes the page (cancel).
    /// </summary>
    Task ReturnToListWithUpdateAsync(PlayFieldSummary? updated);

    /// <summary>
    /// Reads and clears the updated playfield handed back by a successful edit, or <c>null</c> when the
    /// list was reached by any other path. Consumed once by the list page as it re-appears.
    /// </summary>
    PlayFieldSummary? ConsumeUpdated();
}
