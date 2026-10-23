namespace HexMaster.ThePrey.Games.GameEngine;

/// <summary>The work performed for a single game during one sweep tick.</summary>
public sealed record GameTickResult(int Transitions, int Broadcasts, int Penalties, bool Completed)
{
    public static readonly GameTickResult None = new(0, 0, 0, false);
}

/// <summary>
/// Processes one in-progress game for a single sweep tick: status transitions, boundary penalties,
/// last-known-position refresh, and completion. Publishes integration events for everything that
/// changed so the Notifications module can fan them out to clients.
/// </summary>
public interface IGameSweepProcessor
{
    Task<GameTickResult> ProcessAsync(Guid gameId, DateTimeOffset now, CancellationToken ct);
}
