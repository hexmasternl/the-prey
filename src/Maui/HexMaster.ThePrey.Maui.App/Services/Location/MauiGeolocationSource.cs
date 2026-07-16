using Microsoft.Extensions.Logging;
using Microsoft.Maui.Devices.Sensors;

namespace HexMaster.ThePrey.Maui.App.Services.Location;

/// <summary>
/// <see cref="IContinuousLocationSource"/> backed by MAUI <see cref="IGeolocation"/> polling — used on
/// Android (where the foreground service keeps the process alive so polling continues in the background)
/// and on desktop targets (foreground-only). Each tick requests a fresh best-accuracy fix with a short
/// timeout; any permission denial, timeout, or platform error returns <c>null</c> so the coordinator
/// skips the tick rather than stopping. A thin platform adapter — excluded from the unit-test build.
/// </summary>
public sealed class MauiGeolocationSource : IContinuousLocationSource
{
    private static readonly TimeSpan FixTimeout = TimeSpan.FromSeconds(8);

    private readonly IGeolocation _geolocation;
    private readonly ILogger<MauiGeolocationSource> _logger;

    public MauiGeolocationSource(IGeolocation geolocation, ILogger<MauiGeolocationSource> logger)
    {
        _geolocation = geolocation;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken ct = default) => Task.CompletedTask;

    public Task StopAsync() => Task.CompletedTask;

    public async Task<LocationSample?> GetCurrentAsync(CancellationToken ct = default)
    {
        try
        {
            var request = new GeolocationRequest(GeolocationAccuracy.Best, FixTimeout);
            var location = await _geolocation.GetLocationAsync(request, ct);
            if (location is null)
                return null;

            return new LocationSample(
                location.Latitude,
                location.Longitude,
                location.Accuracy,
                location.Timestamp);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogInformation(ex, "Could not acquire a background location fix this tick.");
            return null;
        }
    }
}
