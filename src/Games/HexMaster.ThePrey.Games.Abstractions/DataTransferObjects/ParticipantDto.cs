namespace HexMaster.ThePrey.Games.Abstractions.DataTransferObjects;

public sealed record ParticipantDto(
    Guid UserId,
    string Role,
    GpsCoordinateDto? Location,
    IReadOnlyList<PenaltyDto> Penalties,
    IReadOnlyList<LocationReadingDto> Locations);
