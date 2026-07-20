using HexMaster.ThePrey.Games.DomainModels;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace HexMaster.ThePrey.Games.Data.Postgres;

public sealed class GameRepository : IGameRepository
{
    private readonly GamesDbContext _db;

    public GameRepository(GamesDbContext db) => _db = db;

    public async Task AddAsync(Game game, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(game);
        await _db.Games.AddAsync(game, ct);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsGameCodeViolation(ex))
        {
            // Detach the failed aggregate so the caller can retry with a fresh code on this same scoped context.
            _db.ChangeTracker.Clear();
            throw new DuplicateGameCodeException(game.GameCode, ex);
        }
    }

    private static bool IsGameCodeViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: { } constraint
        } && constraint.Contains(nameof(Game.GameCode), StringComparison.OrdinalIgnoreCase);

    public async Task<Game?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        // Participants and their value objects/history are owned types and load automatically.
        return await _db.Games.FirstOrDefaultAsync(g => g.Id == id, ct);
    }

    public async Task UpdateAsync(Game game, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(game);
        // The aggregate is loaded and tracked within the same scoped context, so change tracking
        // picks up participant/history mutations; persist them.
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<Game>> ListForUserAsync(Guid userId, CancellationToken ct)
    {
        return await _db.Games
            .Where(g => g.OwnerUserId == userId
                     || EF.Property<ICollection<GameParticipant>>(g, "_participants").Any(p => p.UserId == userId))
            .ToListAsync(ct);
    }

    public async Task<Game?> GetActiveGameForUserAsync(Guid userId, CancellationToken ct)
    {
        // Participants are an owned collection mapped via the aggregate's "_participants" backing field
        // (see GameEntityTypeConfiguration). It has no DbSet — owned types are queried through their owner —
        // so reach it inside the predicate with EF.Property, which EF Core translates to a SQL subquery.
        // "Active" is defined by Status, not by timing columns. Arm() moves the game to Started without
        // stamping StartedAt — only the sweep's BeginPlay() does that when it promotes Started to
        // InProgress. A StartedAt-based predicate therefore misses the whole Started window, during which
        // clients must already be routed to their role-specific gameplay page.
        return await _db.Games
            .Where(g => (g.Status == GameStatus.Started || g.Status == GameStatus.InProgress)
                     && EF.Property<ICollection<GameParticipant>>(g, "_participants")
                          .Any(p => p.UserId == userId))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<Game>> GetAllInProgressAsync(CancellationToken ct)
        => await _db.Games.Where(g => g.Status == GameStatus.InProgress).ToListAsync(ct);

    public async Task<IReadOnlyList<Guid>> GetInProgressGameIdsAsync(CancellationToken ct)
        => await _db.Games
            .Where(g => g.Status == GameStatus.Started || g.Status == GameStatus.InProgress)
            .Select(g => g.Id)
            .ToListAsync(ct);

    public async Task<int> DeleteExpiredGamesAsync(DateTimeOffset cutoff, CancellationToken ct)
        => await _db.Games.Where(g => g.CleanUpAfter <= cutoff).ExecuteDeleteAsync(ct);

    public async Task<IReadOnlyList<Game>> GetGamesStartedBetweenAsync(
        DateTimeOffset fromInclusive,
        DateTimeOffset toExclusive,
        CancellationToken ct)
        => await _db.Games
            .Where(g => g.StartedAt != null
                     && g.StartedAt >= fromInclusive
                     && g.StartedAt < toExclusive)
            .ToListAsync(ct);
}
