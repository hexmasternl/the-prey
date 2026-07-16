namespace HexMaster.ThePrey.Maui.App.Services.Realtime;

// Typed payloads for the in-game real-time events. Field names mirror the backend event contract and
// bind case-insensitively (web/camelCase) from each envelope's `data` object.

/// <summary>Payload of <c>state-changed</c>: the game moved to <see cref="NewState"/>.</summary>
public sealed record StateChangedPayload(Guid GameId, string NewState);

/// <summary>Payload of <c>player-location-updated</c>: a participant's latest position and state.</summary>
public sealed record PlayerLocationUpdatedPayload(
    Guid GameId, Guid UserId, double Latitude, double Longitude, string? ParticipantState);

/// <summary>Payload of <c>participant-status-changed</c>: a participant transitioned to <see cref="NewState"/>.</summary>
public sealed record ParticipantStatusChangedPayload(
    Guid GameId, Guid ParticipantId, string? ParticipantRole, string NewState);

/// <summary>Payload of <c>player-penalized</c>: a participant was penalized until <see cref="PenaltyEndsAt"/>.</summary>
public sealed record PlayerPenalizedPayload(
    Guid GameId, Guid UserId, DateTimeOffset PenaltyEndsAt, string? Reason);

/// <summary>Payload of <c>game-ended</c>: the operation concluded with an optional outcome/survivor count.</summary>
public sealed record GameEndedPayload(Guid GameId, string? Outcome, int? SurvivorCount);
