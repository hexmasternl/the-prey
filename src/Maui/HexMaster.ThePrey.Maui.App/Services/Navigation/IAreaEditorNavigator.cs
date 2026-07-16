using HexMaster.ThePrey.Maui.App.Services.Api;

namespace HexMaster.ThePrey.Maui.App.Services.Navigation;

/// <summary>
/// Navigation seam for the area editor, keeping <c>DefineAreaViewModel</c> free of any MAUI/Shell
/// dependency. The editor page reads <see cref="Seed"/> to pre-populate the map with any polygon passed
/// in from the create page, and the view model calls <see cref="ReturnAreaAsync"/> to hand the result
/// back and close the editor.
/// </summary>
public interface IAreaEditorNavigator
{
    /// <summary>The polygon the create page passed in for editing (empty when starting fresh).</summary>
    IReadOnlyList<GpsCoordinate> Seed { get; }

    /// <summary>
    /// Closes the editor and returns to the create page. A non-null <paramref name="points"/> is the saved
    /// polygon; <c>null</c> is a cancel and leaves the create page's held polygon unchanged.
    /// </summary>
    Task ReturnAreaAsync(IReadOnlyList<GpsCoordinate>? points);
}
