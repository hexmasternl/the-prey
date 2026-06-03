namespace HexMaster.ThePrey.PlayFields.Abstractions.DataTransferObjects;

public sealed record PlayFieldDto(
    Guid Id,
    string Name,
    string OwnerId,
    bool IsPublic,
    IReadOnlyList<GpsCoordinateDto> Points);
