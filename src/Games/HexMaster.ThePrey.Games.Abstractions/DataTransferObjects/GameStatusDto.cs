namespace HexMaster.ThePrey.Games.Abstractions.DataTransferObjects;

/// <summary>Rich status of an in-progress game as seen by the requesting user.</summary>
public sealed record GameStatusDto(
    Guid GameId,
    string PlayfieldName,
    IReadOnlyList<GpsCoordinateDto> PlayfieldCoordinates,
    Guid? HunterUserId,
    IReadOnlyList<GameParticipantStatusDto> Participants,
    int GameDurationLeft,
    int NextPingDuration,
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
