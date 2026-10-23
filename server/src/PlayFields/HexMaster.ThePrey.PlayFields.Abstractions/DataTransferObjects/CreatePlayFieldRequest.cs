namespace HexMaster.ThePrey.PlayFields.Abstractions.DataTransferObjects;

public sealed record CreatePlayFieldRequest(
    string Name,
    bool IsPublic,
    IReadOnlyList<GpsCoordinateDto> Points);
