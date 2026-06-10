namespace HexMaster.ThePrey.Games.Abstractions.DataTransferObjects;

/// <summary>
/// The full state of a game, including its lobby and — once started — its hunter and preys.
/// <see cref="IsOwnerPlayer"/> and <see cref="IsReadyToStart"/> are computed from the requesting
/// caller's perspective so the client never has to re-derive ownership or start-eligibility locally.
/// </summary>
public sealed record GameDto(
    Guid Id,
    string GameCode,
    Guid PlayfieldId,
    Guid OwnerUserId,
    string Status,
    GameConfigurationDto Configuration,
    IReadOnlyList<LobbyPlayerDto> Lobby,
    ParticipantDto? Hunter,
    IReadOnlyList<ParticipantDto> Preys,
    DateTimeOffset? StartedAt,
    Guid? DesignatedHunterUserId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? EndsAt,
    DateTimeOffset CleanUpAfter,
    string Outcome,
    DateTimeOffset? CompletedAt,
    bool IsOwnerPlayer,
    bool IsReadyToStart);

/// <summary>A condensed view of a game for list results.</summary>
public sealed record GameSummaryDto(
    Guid Id,
    string GameCode,
    Guid PlayfieldId,
    Guid OwnerUserId,
    string Status,
    int PlayerCount);

/// <summary>
/// The result of recording a location: whether it was accepted and when to report next.
/// <see cref="NextLocationIntervalSeconds"/> is the regular (non-penalty) reporting interval.
/// While the participant has an active penalty, <see cref="PenaltyIntervalSeconds"/> overrides it
/// until <see cref="PenaltyEndsAt"/>; both are null when no penalty is active.
/// </summary>
public sealed record RecordLocationResponse(
    bool Accepted,
    int NextLocationIntervalSeconds,
    int? PenaltyIntervalSeconds = null,
    DateTimeOffset? PenaltyEndsAt = null);

