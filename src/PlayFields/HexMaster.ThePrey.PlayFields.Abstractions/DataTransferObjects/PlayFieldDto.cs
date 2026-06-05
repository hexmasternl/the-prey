namespace HexMaster.ThePrey.PlayFields.Abstractions.DataTransferObjects;

public sealed record PlayFieldDto(
    Guid Id,
    string Name,
    Guid OwnerId,
    bool IsPublic,
    IReadOnlyList<GpsCoordinateDto> Points,
    DateTimeOffset LastUpdatedOn,
    GpsCoordinateDto? CenterCoordinates);
