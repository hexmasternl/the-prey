namespace HexMaster.ThePrey.PlayFields.Abstractions.DataTransferObjects;

public sealed record UpsertPlayFieldRequest(
    string Name,
    bool IsPublic,
    IReadOnlyList<GpsCoordinateDto> Points,
    DateTimeOffset LastUpdatedOn);
