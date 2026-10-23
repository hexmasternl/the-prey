namespace HexMaster.ThePrey.Games.Abstractions.DataTransferObjects;

/// <summary>Complete export of a single game, including all participants, GPS pings, and penalties.</summary>
public sealed record GameExportDto(
    Guid Id,
    string GameCode,
    Guid PlayfieldId,
    Guid OwnerUserId,
    string Status,
    GameConfigurationDto Configuration,
    DateTimeOffset? StartedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset? EndsAt,
    DateTimeOffset CleanUpAfter,
    DateTimeOffset? CompletedAt,
    string Outcome,
    Guid? HunterUserId,
    IReadOnlyList<Guid> Preys,
    IReadOnlyList<ParticipantExportDto> Participants);

/// <summary>Full participant snapshot, including role flag, all GPS pings, and all penalties.</summary>
public sealed record ParticipantExportDto(
    Guid UserId,
    string DisplayName,
    string? ProfilePictureUrl,
    bool IsReady,
    string State,
    DateTimeOffset? LastLocationAt,
    GpsCoordinateDto? Location,
    GpsCoordinateDto? DelayAnchorLocation,
    bool DelayPenaltyApplied,
    bool IsHunter,
    IReadOnlyList<PenaltyExportDto> Penalties,
    IReadOnlyList<LocationReadingExportDto> Locations);

/// <summary>A single penalty applied to a participant.</summary>
public sealed record PenaltyExportDto(Guid Id, DateTimeOffset EndsAt);

/// <summary>A single GPS location reading recorded for a participant.</summary>
public sealed record LocationReadingExportDto(
    Guid Id,
    GpsCoordinateDto Coordinate,
    DateTimeOffset RecordedAt,
    bool Checked);
