namespace HexMaster.ThePrey.Maui.App.Services.Navigation;

/// <summary>
/// Abstracts Shell route navigation so view models stay free of MAUI types and remain unit-testable.
/// </summary>
public interface IMenuNavigator
{
    /// <summary>Pushes <paramref name="route"/> onto the navigation stack, leaving the current page beneath it.</summary>
    Task GoToAsync(string route);

    /// <summary>
    /// Navigates to <paramref name="route"/> replacing the current page, so it is removed from the
    /// navigation stack. Used for one-way hand-offs the user must not be able to back out of — notably
    /// lobby → gameplay, where returning to a lobby whose game has already started makes no sense.
    /// </summary>
    Task ReplaceAsync(string route);
}
