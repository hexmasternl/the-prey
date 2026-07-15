using HexMaster.ThePrey.Maui.App.Services.Api;

namespace HexMaster.ThePrey.Maui.App.Services.Navigation;

/// <summary>
/// Navigation seam for the Create Playfield flow, keeping <c>CreatePlayfieldViewModel</c> free of any
/// MAUI/Shell dependency (and so unit-testable). Implements the design's result hand-off: opening the
/// area editor and awaiting the edited polygon, and closing the create page back to the list with the
/// created playfield (or nothing on cancel).
/// </summary>
public interface ICreatePlayfieldNavigator
{
    /// <summary>
    /// Opens the area editor seeded with <paramref name="current"/> and resolves with the edited polygon,
    /// or <c>null</c> when the editor was cancelled (leaving the caller's polygon unchanged).
    /// </summary>
    Task<IReadOnlyList<GpsCoordinate>?> DefineAreaAsync(IReadOnlyList<GpsCoordinate> current);

    /// <summary>
    /// Closes the create page and returns to the list. When <paramref name="created"/> is non-null it is
    /// handed to the list to append; <c>null</c> just closes the page (cancel) leaving the list unchanged.
    /// </summary>
    Task ReturnToListAsync(PlayFieldSummary? created);

    /// <summary>
    /// Reads and clears the last created playfield handed back by a successful create, or <c>null</c> when
    /// the list was reached by any other path. Consumed once by the list page as it re-appears.
    /// </summary>
    PlayFieldSummary? ConsumeCreated();
}
