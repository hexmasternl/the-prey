using HexMaster.ThePrey.Games.DomainModels;

namespace HexMaster.ThePrey.Games;

public interface IGameRepository
{
    Task AddAsync(Game game, CancellationToken ct);

    Task<Game?> GetByIdAsync(Guid id, CancellationToken ct);

    Task UpdateAsync(Game game, CancellationToken ct);

    /// <summary>Returns the games owned by the given user plus the games whose lobby they have joined.</summary>
    Task<IReadOnlyList<Game>> ListForUserAsync(Guid userId, CancellationToken ct);

    /// <summary>Returns the first InProgress game where the user is the owner, a lobby member, or a participant.</summary>
    Task<Game?> GetActiveGameForUserAsync(Guid userId, CancellationToken ct);

    /// <summary>Returns all games currently in the InProgress state.</summary>
    Task<IReadOnlyList<Game>> GetAllInProgressAsync(CancellationToken ct);

    /// <summary>
    /// Returns the ids of all games currently in the InProgress state. Used by the sweep so each game
    /// can be loaded on its own DbContext for safe parallel processing.
    /// </summary>
    Task<IReadOnlyList<Guid>> GetInProgressGameIdsAsync(CancellationToken ct);

    /// <summary>Hard-deletes all games whose CleanUpAfter is at or before <paramref name="cutoff"/>. Returns the number of deleted rows.</summary>
    Task<int> DeleteExpiredGamesAsync(DateTimeOffset cutoff, CancellationToken ct);
}
