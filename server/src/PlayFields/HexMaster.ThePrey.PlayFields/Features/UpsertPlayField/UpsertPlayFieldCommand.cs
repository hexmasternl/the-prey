using HexMaster.ThePrey.PlayFields.Abstractions.DataTransferObjects;

namespace HexMaster.ThePrey.PlayFields.Features.UpsertPlayField;

public sealed record UpsertPlayFieldCommand(
    Guid Id,
    Guid OwnerId,
    string Name,
    bool IsPublic,
    IReadOnlyList<GpsCoordinateDto> Points,
    DateTimeOffset LastUpdatedOn);

public abstract record UpsertPlayFieldResult
{
    public sealed record Created(PlayFieldDto PlayField) : UpsertPlayFieldResult;
    public sealed record Updated(PlayFieldDto PlayField) : UpsertPlayFieldResult;
    public sealed record Conflict(PlayFieldDto CurrentPlayField) : UpsertPlayFieldResult;
    public sealed record Forbidden : UpsertPlayFieldResult;
}
