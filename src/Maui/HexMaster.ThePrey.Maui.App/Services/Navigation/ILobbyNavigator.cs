namespace HexMaster.ThePrey.Maui.App.Services.Navigation;

/// <summary>
/// The lobby's onward hand-off seam: once the game starts (owner's start succeeds, or a
/// <c>configuration-changed</c> delta reports Started/InProgress to a non-owner) the lobby navigates to the
/// gameplay screen through this interface. The concrete gameplay destination is owned by a separate change;
/// keeping it behind an interface leaves the lobby view model free of MAUI types and unit-testable.
/// </summary>
public interface ILobbyNavigator
{
    Task GoToGameplayAsync();
}
