using CoreLocation;
using Foundation;
using Microsoft.Extensions.Logging;

namespace HexMaster.ThePrey.Maui.App.Services.Location;

/// <summary>
/// iOS adapter that is both the <see cref="IBackgroundExecutionHost"/> and the
/// <see cref="IContinuousLocationSource"/>: a single <see cref="CLLocationManager"/> with
/// <c>AllowsBackgroundLocationUpdates=true</c> and <c>PausesLocationUpdatesAutomatically=false</c> keeps
/// the app running in the background (the sanctioned way on iOS, which suspends idle apps) while pushing
/// fixes via <see cref="CLLocationManager.LocationsUpdated"/>. The newest fix is cached and returned from
/// <see cref="GetCurrentAsync"/>; the coordinator's cadence timer gates how often it is actually reported,
/// so exact wall-clock cadence is approximated (see the design's iOS note). Requests "Always"
/// authorization on start; a denial degrades to foreground-only rather than failing the game.
/// </summary>
public sealed class IosBackgroundLocationManager : NSObject, IBackgroundExecutionHost, IContinuousLocationSource
{
    private readonly ILogger<IosBackgroundLocationManager> _logger;
    private readonly object _sync = new();

    private CLLocationManager? _manager;
    private LocationSample? _latest;
    private bool _running;

    public IosBackgroundLocationManager(ILogger<IosBackgroundLocationManager> logger) => _logger = logger;

    public Task StartAsync(CancellationToken ct = default)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            try
            {
                if (_running)
                    return;
                _running = true;

                _manager ??= new CLLocationManager();
                _manager.DesiredAccuracy = CLLocation.AccuracyBest;
                _manager.AllowsBackgroundLocationUpdates = true;
                _manager.PausesLocationUpdatesAutomatically = false;
                _manager.ShowsBackgroundLocationIndicator = true;

                _manager.LocationsUpdated -= OnLocationsUpdated;
                _manager.LocationsUpdated += OnLocationsUpdated;

                // "Always" is required for screen-off/background tracking; a denial leaves us with
                // when-in-use (foreground-only), which still lets the game run.
                _manager.RequestAlwaysAuthorization();
                _manager.StartUpdatingLocation();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to start iOS background location updates.");
            }
        });

        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            try
            {
                if (_manager is not null)
                {
                    _manager.LocationsUpdated -= OnLocationsUpdated;
                    _manager.StopUpdatingLocation();
                    _manager.AllowsBackgroundLocationUpdates = false;
                }

                _running = false;
                lock (_sync) _latest = null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to stop iOS background location updates.");
            }
        });

        return Task.CompletedTask;
    }

    public Task<LocationSample?> GetCurrentAsync(CancellationToken ct = default)
    {
        lock (_sync)
            return Task.FromResult(_latest);
    }

    private void OnLocationsUpdated(object? sender, CLLocationsUpdatedEventArgs e)
    {
        var location = e.Locations.LastOrDefault();
        if (location is null)
            return;

        // HorizontalAccuracy is negative when the fix is invalid; surface null in that case.
        double? accuracy = location.HorizontalAccuracy >= 0 ? location.HorizontalAccuracy : null;
        // SecondsSince1970 gives an unambiguous absolute time (avoids NSDate→DateTime timezone pitfalls).
        var recordedAt = DateTimeOffset.FromUnixTimeMilliseconds((long)(location.Timestamp.SecondsSince1970 * 1000));

        lock (_sync)
        {
            _latest = new LocationSample(
                location.Coordinate.Latitude,
                location.Coordinate.Longitude,
                accuracy,
                recordedAt);
        }
    }
}
