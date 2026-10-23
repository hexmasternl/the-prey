namespace HexMaster.ThePrey.Games.GameEngine;

/// <summary>
/// Runs one sweep tick across every in-progress game. Trigger-agnostic: invoked by the in-process
/// <c>GameTickService</c> timer today, but could equally be invoked by a Dapr cron binding hitting an
/// internal endpoint. Acquires sweep leadership first so only one replica does the work, and is
/// non-reentrant so a slow tick never overlaps the next.
/// </summary>
public interface IGameTickRunner
{
    Task RunTickAsync(CancellationToken ct);
}
