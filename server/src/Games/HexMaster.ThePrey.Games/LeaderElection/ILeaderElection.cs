namespace HexMaster.ThePrey.Games.LeaderElection;

/// <summary>
/// Coordinates which single Games API replica runs the periodic game sweep. When the API scales to
/// multiple replicas, only the replica that holds leadership processes games — the others stand by.
/// The default implementation uses a PostgreSQL session-level advisory lock; leadership is released
/// automatically if the holder dies, letting a standby take over on its next attempt.
/// </summary>
public interface ILeaderElection
{
    /// <summary>
    /// Attempts to acquire (or confirm continued) leadership for this tick.
    /// Returns true when this replica is the leader and should run the sweep.
    /// </summary>
    Task<bool> TryAcquireAsync(CancellationToken ct);
}
