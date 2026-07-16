using HexMaster.ThePrey.Maui.App.Services.Api;

namespace HexMaster.ThePrey.Maui.App.Services.Realtime;

/// <summary>
/// The single composite snapshot of the active game the in-game UI renders, owned and published by
/// <see cref="IGameStateService"/>. It folds together the reads the gameplay screens need — the game
/// record (status / hunter / roster), the rich in-progress status (playfield polygon, per-participant
/// last-known location, the head-start moment, the clock and ping cadence), and the role-specific state
/// (prey distance / prey locations) — into one value the map and HUD both read. Real-time events mutate
/// the participant/status slices in place; a periodic and on-(re)connect reconcile refreshes the whole.
/// </summary>
public sealed record GameLiveState
{
    /// <summary>The game this snapshot describes.</summary>
    public required Guid GameId { get; init; }

    /// <summary>Backend status: <c>Lobby</c> / <c>Ready</c> / <c>InProgress</c> / <c>Completed</c>.</summary>
    public string Status { get; init; } = "Lobby";

    /// <summary>The designated hunter, or <c>null</c> before one is picked.</summary>
    public Guid? HunterUserId { get; init; }

    /// <summary>Every participant (hunter + preys) with its latest state and last-known location.</summary>
    public IReadOnlyList<GameLiveParticipant> Participants { get; init; } = [];

    /// <summary>The playfield polygon vertices; empty until the in-progress status has been read.</summary>
    public IReadOnlyList<GpsCoordinate> PlayfieldCoordinates { get; init; } = [];

    /// <summary>The instant the hunter may start moving (drives the head-start countdown), or <c>null</c>.</summary>
    public DateTimeOffset? HunterMayMoveAt { get; init; }

    /// <summary>Seconds left in the game at the last reconcile; the HUD ticks this down locally.</summary>
    public int GameDurationLeft { get; init; }

    /// <summary>Seconds until the next location broadcast at the last reconcile; ticked down locally.</summary>
    public int NextPingDuration { get; init; }

    /// <summary>The full ping interval (seconds) — the denominator for the HUD's next-ping bar.</summary>
    public int CurrentPingInterval { get; init; }

    /// <summary>True once the game has entered its final stage.</summary>
    public bool IsEndgame { get; init; }

    /// <summary>Active (untagged) preys remaining; recomputed as participant states change.</summary>
    public int PreysLeft { get; init; }

    /// <summary>Server-computed distance to the hunter for a prey viewer, or <c>null</c> when unknown.</summary>
    public int? HunterDistanceMeters { get; init; }

    /// <summary>Prey locations supplied to a hunter viewer by the role-specific state read.</summary>
    public IReadOnlyList<GpsCoordinate> PreyLocations { get; init; } = [];

    /// <summary>True while the game is actively running.</summary>
    public bool IsInProgress => string.Equals(Status, "InProgress", StringComparison.OrdinalIgnoreCase);

    /// <summary>True once the game has concluded.</summary>
    public bool IsCompleted => string.Equals(Status, "Completed", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// One participant in <see cref="GameLiveState"/>: identity, current <see cref="State"/>
/// (<c>Active</c>/<c>Passive</c> vs <c>Tagged</c>/<c>Out</c>), last-known <see cref="Location"/> (<c>null</c>
/// until a position has been broadcast), and any active boundary <see cref="PenaltyEndsAt"/>. DisplayName is
/// intentionally absent — the map and HUD key on ids and colors, and the tag flow fetches names separately.
/// </summary>
public sealed record GameLiveParticipant(
    Guid UserId,
    string State,
    GpsCoordinate? Location,
    DateTimeOffset? PenaltyEndsAt = null);
