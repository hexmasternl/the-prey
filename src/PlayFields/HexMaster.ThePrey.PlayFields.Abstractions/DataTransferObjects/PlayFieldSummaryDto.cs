namespace HexMaster.ThePrey.PlayFields.Abstractions.DataTransferObjects;

public sealed record PlayFieldSummaryDto(
    Guid Id,
    string Name,
    string OwnerId,
    bool IsPublic,
    DateTimeOffset LastUpdatedOn,
    GpsCoordinateDto? CenterCoordinates);
