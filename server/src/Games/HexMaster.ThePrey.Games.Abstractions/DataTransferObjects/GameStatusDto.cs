namespace HexMaster.ThePrey.Games.Abstractions.DataTransferObjects;

/// <summary>Rich status of an in-progress game as seen by the requesting user.</summary>
public sealed record GameStatusDto(
    Guid GameId,
    string PlayfieldName,
    IReadOnlyList<GpsCoordinateDto> PlayfieldCoordinates,
    Guid? HunterUserId,
    IReadOnlyList<GameParticipantStatusDto> Participants,
    int GameDurationLeft,
    /// <summary>
    /// Server-calculated whole seconds remaining until the next update for this participant.
    /// The client seeds its countdown bar from this value and ticks it down locally between polls.
    /// Non-penalised players share the game-wide schedule (same value at the same instant for all);
    /// penalised players use a personal cadence anchored to their last recorded location.
    /// </summary>
    int NextPingDuration,
    /// <summary>
    /// Whole seconds remaining until the next sweep tick for a penalised participant, clamped to
    /// [0, 30]. The client uses this to seed a fixed-30-second penalty countdown bar.
    /// Always 0 for non-penalised participants and non-participants.
    /// </summary>
    int NextPingDurationWithPenalty,
    /// <summary>The participant's current reporting interval in whole seconds — the full duration between consecutive scheduled pings; 0 for non-participants.</summary>
    int CurrentPingInterval,
    bool IsEndgame,
    int PreysLeft,
    DateTimeOffset? HunterMayMoveAt);

/// <summary>The status and last known location of a single game participant.</summary>
public sealed record GameParticipantStatusDto(
    Guid UserId,
    string Callsign,
    GpsCoordinateDto? LastKnownLocation,
    bool HasActivePenalty,
    string State);
