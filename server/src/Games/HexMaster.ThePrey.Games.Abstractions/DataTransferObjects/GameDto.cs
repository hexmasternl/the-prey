namespace HexMaster.ThePrey.Games.Abstractions.DataTransferObjects;

/// <summary>
/// The full state of a game, with a single unified participants list.
/// <see cref="HunterUserId"/> identifies the hunter within that list.
/// <see cref="Preys"/> is a derived list of every participant's UserId except the hunter.
/// <see cref="IsOwnerPlayer"/> and <see cref="IsReadyToStart"/> are caller-perspective flags.
/// </summary>
public sealed record GameDto(
    Guid Id,
    string GameCode,
    Guid PlayfieldId,
    Guid OwnerUserId,
    string Status,
    GameConfigurationDto Configuration,
    IReadOnlyList<ParticipantDto> Participants,
    Guid? HunterUserId,
    IReadOnlyList<Guid> Preys,
    DateTimeOffset? StartedAt,
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
