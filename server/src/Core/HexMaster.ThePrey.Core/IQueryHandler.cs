namespace HexMaster.ThePrey.Core;

public interface IQueryHandler<TQuery, TResult>
{
    Task<TResult> Handle(TQuery query, CancellationToken ct);
}
