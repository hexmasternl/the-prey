namespace HexMaster.ThePrey.Games.Abstractions.DataTransferObjects;

/// <summary>Unified view of a game participant — covers both lobby and in-progress states.</summary>
public sealed record ParticipantDto(
    Guid UserId,
    string DisplayName,
    string? ProfilePictureUrl,
    bool IsReady,
    string State,
    GpsCoordinateDto? LastKnownLocation,
    bool HasActivePenalty);
