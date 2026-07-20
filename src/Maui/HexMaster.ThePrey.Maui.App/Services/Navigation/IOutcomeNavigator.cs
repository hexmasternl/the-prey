namespace HexMaster.ThePrey.Maui.App.Services.Navigation;

/// <summary>
/// The post-game hand-off seam: gameplay → outcome screen, and outcome screen → main menu. Kept behind
/// an interface so the gameplay and outcome view models stay free of MAUI navigation types and testable.
/// </summary>
public interface IOutcomeNavigator
{
    /// <summary>
    /// Navigates to the outcome screen for <paramref name="gameId"/>, carrying the caller's best knowledge
    /// of the local player's role. Idempotent per game: a second call for a game already handed off is a
    /// no-op, so a late status poll racing the real-time game-ended event cannot stack a second page.
    /// </summary>
    Task GoToOutcomeAsync(Guid gameId, bool isHunter);

    /// <summary>
    /// Returns to the main menu, clearing the finished game's pages (outcome, gameplay, lobby) from the
    /// navigation stack so the platform back gesture cannot re-enter a dead game.
    /// </summary>
    Task ReturnToMenuAsync();
}
