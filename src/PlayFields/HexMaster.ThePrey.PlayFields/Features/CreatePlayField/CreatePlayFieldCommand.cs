using HexMaster.ThePrey.PlayFields.Abstractions.DataTransferObjects;

namespace HexMaster.ThePrey.PlayFields.Features.CreatePlayField;

public sealed record CreatePlayFieldCommand(
    string OwnerId,
    string Name,
    bool IsPublic,
    IReadOnlyList<GpsCoordinateDto> Points);

public sealed record CreatePlayFieldResult(PlayFieldDto PlayField);
