namespace HexMaster.ThePrey.Maui.App.Services.Navigation;

/// <summary>
/// The gameplay hand-off seam. Fulfils the lobby's post-start (and Resume) hand-off by resolving the
/// active game's role and routing to the hunter or prey game page (<see cref="ILobbyNavigator.GoToGameplayAsync"/>),
/// and provides the once-only game-ended hand-off to the outcome screen invoked by the gameplay view
/// models. Kept behind an interface so the view models stay free of MAUI navigation types and testable.
/// </summary>
public interface IGameplayNavigator
{
    /// <summary>Navigates to the game-outcome screen after game-ended (or a Completed snapshot).</summary>
    Task GoToOutcomeAsync();
}
