namespace HexMaster.ThePrey.PlayFields.Features.DeletePlayField;

public sealed record DeletePlayFieldCommand(Guid PlayFieldId, Guid OwnerId);

public abstract record DeletePlayFieldResult
{
    public sealed record Success : DeletePlayFieldResult;
    public sealed record NotFound : DeletePlayFieldResult;
    public sealed record Forbidden : DeletePlayFieldResult;
}
