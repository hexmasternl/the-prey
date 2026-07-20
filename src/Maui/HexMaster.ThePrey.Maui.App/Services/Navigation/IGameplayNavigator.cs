namespace HexMaster.ThePrey.Maui.App.Services.Navigation;

/// <summary>
/// The gameplay hand-off seam. Fulfils the lobby's post-start (and Resume) hand-off by resolving the
/// active game's role and routing to the hunter or prey game page (<see cref="ILobbyNavigator.GoToGameplayAsync"/>),
/// and provides the once-only game-ended hand-off to the outcome screen invoked by the gameplay view
/// models. Kept behind an interface so the view models stay free of MAUI navigation types and testable.
/// </summary>
public interface IGameplayNavigator
{
    /// <summary>
    /// Resumes an ongoing game from the main menu: resolves the caller's role and navigates straight to
    /// the hunter or prey page, never the lobby (<c>GET /games/active</c> only ever returns a Started or
    /// InProgress game, so a resumable game has always left the lobby). Unlike the lobby's hand-off this
    /// <em>pushes</em>, leaving the menu beneath it — the menu is the Shell root and must not be popped.
    /// </summary>
    Task ResumeGameplayAsync();

    /// <summary>
    /// Navigates to the game-outcome screen after game-ended (or a Completed snapshot), carrying the
    /// finished game and the local player's role. Idempotent per game — see
    /// <see cref="IOutcomeNavigator.GoToOutcomeAsync"/>, which fulfils this.
    /// </summary>
    Task GoToOutcomeAsync(Guid gameId, bool isHunter);
}
