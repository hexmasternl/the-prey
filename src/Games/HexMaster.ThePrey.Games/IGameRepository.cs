using HexMaster.ThePrey.Games.DomainModels;

namespace HexMaster.ThePrey.Games;

public interface IGameRepository
{
    Task AddAsync(Game game, CancellationToken ct);

    Task<Game?> GetByIdAsync(Guid id, CancellationToken ct);

    Task UpdateAsync(Game game, CancellationToken ct);

    /// <summary>Returns the games owned by the given user plus the games whose lobby they have joined.</summary>
    Task<IReadOnlyList<Game>> ListForUserAsync(Guid userId, CancellationToken ct);
}
