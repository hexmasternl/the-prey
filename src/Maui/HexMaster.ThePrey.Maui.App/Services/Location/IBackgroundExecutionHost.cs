namespace HexMaster.ThePrey.Maui.App.Services.Location;

/// <summary>
/// Platform adapter that starts and stops the OS mechanism keeping the process alive while tracking,
/// so the coordinator's cadence loop keeps running with the app backgrounded or the screen locked.
/// On Android this is a foreground service of type <c>location</c> with a persistent notification;
/// on iOS it is continuous <c>CLLocationManager</c> background updates. Windows/MacCatalyst use a
/// no-op host (foreground-only). Implementations request the required runtime permissions here and
/// degrade gracefully rather than throwing when background execution is unavailable.
/// </summary>
public interface IBackgroundExecutionHost
{
    /// <summary>Starts (or re-arms) the keep-alive mechanism. Idempotent; never throws.</summary>
    Task StartAsync(CancellationToken ct = default);

    /// <summary>Tears down the keep-alive mechanism, removing any persistent notification. Idempotent.</summary>
    Task StopAsync();
}
