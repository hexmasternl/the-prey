using HexMaster.ThePrey.Maui.App.Services.Api;

namespace HexMaster.ThePrey.Maui.App.Services.Realtime;

// Typed payloads for the canonical real-time event catalog. Field names mirror the backend event contract
// (HexMaster.ThePrey.Games.Abstractions.DataTransferObjects.RealtimeMessages) and bind case-insensitively
// (web/camelCase) from each envelope's `data` object.

/// <summary>
/// Full participant snapshot carried by <c>participant-joined</c>/<c>participant-changed</c>. Replacing a
/// participant entry wholesale with this payload never loses a field for that participant, since every
/// field the app tracks is present here (the wire payload's <c>profilePictureUrl</c> is not modelled — the
/// app does not render avatars). <see cref="HasActivePenalty"/> is a flag only — the exact
/// <c>penaltyEndsAt</c> instant arrives (and is cleared) via <c>prey-updated</c>.
/// </summary>
public sealed record ParticipantPayload(
    Guid UserId,
    string DisplayName,
    bool IsReady,
    string State,
    GpsCoordinate? LastKnownLocation,
    bool HasActivePenalty);

/// <summary>Payload of <c>participant-removed</c>.</summary>
public sealed record ParticipantRemovedPayload(Guid UserId);

/// <summary>
/// Payload of <c>configuration-changed</c>: the game-level slice (status, configuration, hunter,
/// ownership, timing). Deliberately omits the participant list and the caller-perspective flags
/// (<c>isOwnerPlayer</c>, <c>isReadyToStart</c>) — those are preserved from the current state, never
/// overwritten by this delta. The wire payload's <c>playfieldId</c>/<c>preys</c>/timing fields beyond
/// <c>completedAt</c> are not modelled — nothing the app currently renders reads them.
/// </summary>
public sealed record ConfigurationChangedPayload(
    Guid Id,
    string GameCode,
    Guid OwnerUserId,
    string Status,
    GameConfigurationDetails? Configuration,
    Guid? HunterUserId,
    string? Outcome,
    DateTimeOffset? CompletedAt);

/// <summary>One entry of a <see cref="LocationsUpdatedPayload"/> batch. <see cref="Role"/> is <c>"Hunter"</c> or <c>"Prey"</c>.</summary>
public sealed record LocationEntry(Guid UserId, string Role, double Latitude, double Longitude, string State);

/// <summary>Payload of <c>locations-updated</c>: one or more batched location entries per sweep tick.</summary>
public sealed record LocationsUpdatedPayload(IReadOnlyList<LocationEntry> Locations);

/// <summary>Payload of <c>prey-updated</c>. <see cref="Event"/> is one of <see cref="GameRealtimeEventTypes.PreyEvents"/>.</summary>
public sealed record PreyUpdatedPayload(
    Guid UserId, string Event, string? State, DateTimeOffset? PenaltyEndsAt, string? Reason);

/// <summary>Payload of <c>game-ended</c>. Emitted exactly once per game.</summary>
public sealed record GameEndedPayload(string Outcome, int SurvivorCount, DateTimeOffset? CompletedAt);

/// <summary>Payload of <c>resync-requested</c>.</summary>
public sealed record ResyncRequestedPayload(string? Reason);
