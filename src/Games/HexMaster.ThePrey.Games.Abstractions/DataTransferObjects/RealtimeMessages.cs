namespace HexMaster.ThePrey.Games.Abstractions.DataTransferObjects;

// The wire payload shapes for the canonical real-time message catalog (see
// HexMaster.ThePrey.IntegrationEvents.RealtimeProtocol). Each of these is the `data` of a versioned
// envelope broadcast to a game's Web PubSub group. They are deliberately distinct from the REST
// snapshot DTOs (GameDto, ParticipantDto) so the wire contract can evolve independently and never
// leaks caller-perspective fields into a group broadcast.

/// <summary>
/// The <c>configuration-changed</c> payload: the full game-level slice (status, configuration, hunter,
/// timing) excluding <see cref="GameDto.Participants"/> and the caller-perspective flags
/// (<see cref="GameDto.IsOwnerPlayer"/>, <see cref="GameDto.IsReadyToStart"/>), which are wrong over a
/// broadcast — clients derive ownership/readiness locally.
/// </summary>
public sealed record GameConfigurationChangedDto(
    Guid Id,
    string GameCode,
    Guid PlayfieldId,
    Guid OwnerUserId,
    string Status,
    GameConfigurationDto Configuration,
    Guid? HunterUserId,
    IReadOnlyList<Guid> Preys,
    DateTimeOffset? StartedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset? EndsAt,
    DateTimeOffset CleanUpAfter,
    string Outcome,
    DateTimeOffset? CompletedAt);

/// <summary>One entry of a <see cref="LocationsUpdatedDto"/> batch. <see cref="Role"/> is <c>"Hunter"</c> or <c>"Prey"</c>.</summary>
public sealed record ParticipantLocationDto(
    Guid UserId,
    string Role,
    double Latitude,
    double Longitude,
    string State);

/// <summary>The <c>locations-updated</c> payload: one or more batched location entries per sweep tick.</summary>
public sealed record LocationsUpdatedDto(IReadOnlyList<ParticipantLocationDto> Locations);

/// <summary>
/// The <c>prey-updated</c> payload. <see cref="Event"/> is one of
/// <c>HexMaster.ThePrey.IntegrationEvents.RealtimeProtocol.PreyEvents</c>.
/// </summary>
public sealed record PreyUpdatedDto(
    Guid UserId,
    string Event,
    string? State,
    DateTimeOffset? PenaltyEndsAt,
    string? Reason);

/// <summary>The <c>game-ended</c> payload. Emitted exactly once per game.</summary>
public sealed record GameEndedNotificationDto(
    string Outcome,
    int SurvivorCount,
    DateTimeOffset? CompletedAt);
