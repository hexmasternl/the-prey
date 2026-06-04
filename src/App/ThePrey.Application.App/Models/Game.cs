namespace ThePrey.Application.App.Models;

/// <summary>
/// A game as returned by the Games API (<c>POST /games</c>, <c>GET /games/{id}</c>):
/// identity, shareable 8-digit code, status, lobby, and configuration.
/// </summary>
public sealed class Game
{
    public Guid Id { get; set; }
    public string GameCode { get; set; } = string.Empty;
    public Guid PlayfieldId { get; set; }
    public Guid OwnerUserId { get; set; }
    public string Status { get; set; } = string.Empty;
    public GameConfigurationInfo? Configuration { get; set; }
    public List<GameLobbyPlayer> Lobby { get; set; } = [];
    public DateTimeOffset? StartedAt { get; set; }

    /// <summary>True while the game is gathering players and has not been started.</summary>
    public bool IsInLobby => string.Equals(Status, "Lobby", StringComparison.OrdinalIgnoreCase);
}

/// <summary>A player in a game's lobby.</summary>
public sealed class GameLobbyPlayer
{
    public Guid UserId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? ProfilePictureUrl { get; set; }
}

/// <summary>
/// Game tuning values as the API carries them: durations in minutes, location intervals in seconds.
/// </summary>
public sealed class GameConfigurationInfo
{
    public int GameDuration { get; set; }
    public int HunterDelayTime { get; set; }
    public int FinalStageDuration { get; set; }
    public int DefaultLocationInterval { get; set; }
    public int FinalLocationInterval { get; set; }
    public bool EnablePreyBoundaryPenalties { get; set; }
    public bool EnableHunterBoundaryPenalty { get; set; }
}
