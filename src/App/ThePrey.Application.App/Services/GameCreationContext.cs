using ThePrey.Application.App.Models;

namespace ThePrey.Application.App.Services;

/// <summary>
/// Singleton state shared between the create-game pages: carries the game returned by the server
/// from the Start Game page to the lobby and Game Progress pages (mirrors <see cref="PlayfieldEditingContext"/>).
/// </summary>
public sealed class GameCreationContext
{
    /// <summary>The game being set up, or null when no create-game flow is active.</summary>
    public Game? CurrentGame { get; set; }

    public void Clear() => CurrentGame = null;
}
