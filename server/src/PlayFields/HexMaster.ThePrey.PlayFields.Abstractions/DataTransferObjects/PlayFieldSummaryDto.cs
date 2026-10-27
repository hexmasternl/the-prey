namespace HexMaster.ThePrey.PlayFields.Abstractions.DataTransferObjects;

/// <param name="DistanceMeters">
/// Distance in whole metres from the searched position to <paramref name="CenterCoordinates"/>; only
/// set by a public search with coordinates, null everywhere else.
/// </param>
public sealed record PlayFieldSummaryDto(
    Guid Id,
    string Name,
    Guid OwnerId,
    bool IsPublic,
    DateTimeOffset LastUpdatedOn,
    GpsCoordinateDto? CenterCoordinates,
    int? DistanceMeters = null);
