namespace HexMaster.ThePrey.PlayFields.Abstractions.DataTransferObjects;

public sealed record PlayFieldSummaryDto(
    Guid Id,
    string Name,
    Guid OwnerId,
    bool IsPublic,
    DateTimeOffset LastUpdatedOn,
    GpsCoordinateDto? CenterCoordinates);
