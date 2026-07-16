namespace HexMaster.ThePrey.Maui.App.Services.Location;

/// <summary>
/// Platform adapter that supplies the device's current position while tracking. The coordinator pulls
/// one sample per cadence tick via <see cref="GetCurrentAsync"/>. Adapters that receive OS-pushed
/// updates (iOS <c>CLLocationManager</c>) cache the newest fix and return it here; adapters that poll
/// (MAUI <c>IGeolocation</c> on Android/desktop) read on demand. A failed or unavailable fix returns
/// <c>null</c> so the coordinator skips the tick without stopping — it never throws.
/// </summary>
public interface IContinuousLocationSource
{
    /// <summary>Starts delivering/collecting fixes. Idempotent; never throws.</summary>
    Task StartAsync(CancellationToken ct = default);

    /// <summary>Stops delivering/collecting fixes. Idempotent.</summary>
    Task StopAsync();

    /// <summary>The current position, or <c>null</c> when no fix is available for this tick.</summary>
    Task<LocationSample?> GetCurrentAsync(CancellationToken ct = default);
}
