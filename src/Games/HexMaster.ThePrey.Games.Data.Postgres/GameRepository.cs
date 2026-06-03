using HexMaster.ThePrey.Games.DomainModels;
using Microsoft.EntityFrameworkCore;

namespace HexMaster.ThePrey.Games.Data.Postgres;

public sealed class GameRepository : IGameRepository
{
    private readonly GamesDbContext _db;

    public GameRepository(GamesDbContext db) => _db = db;

    public async Task AddAsync(Game game, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(game);
        await _db.Games.AddAsync(game, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<Game?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        // The lobby, participants, and their value objects/history are owned types and load automatically.
        return await _db.Games.FirstOrDefaultAsync(g => g.Id == id, ct);
    }

    public async Task UpdateAsync(Game game, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(game);
        // The aggregate is loaded and tracked within the same scoped context, so change tracking
        // picks up lobby/participant/history mutations; persist them.
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<Game>> ListForUserAsync(Guid userId, CancellationToken ct)
    {
        return await _db.Games
            .Where(g => g.OwnerUserId == userId || g.Lobby.Any(p => p.UserId == userId))
            .ToListAsync(ct);
    }
}
