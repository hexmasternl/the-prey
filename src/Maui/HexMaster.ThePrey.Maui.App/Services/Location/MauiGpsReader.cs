using Microsoft.Extensions.Logging;

namespace HexMaster.ThePrey.Maui.App.Services.Location;

/// <summary>
/// Default <see cref="IGpsReader"/> backed by MAUI <see cref="IGeolocation"/>. Requests
/// when-in-use permission, then returns the last-known location (falling back to a low-accuracy
/// fix). Permission and location APIs are marshalled to the main thread. Any denial, timeout, or
/// error yields <c>null</c> so the menu shows the placeholder rather than an error.
/// </summary>
public sealed class MauiGpsReader : IGpsReader
{
    private static readonly TimeSpan FixTimeout = TimeSpan.FromSeconds(10);

    private readonly IGeolocation _geolocation;
    private readonly ILogger<MauiGpsReader> _logger;

    public MauiGpsReader(IGeolocation geolocation, ILogger<MauiGpsReader> logger)
    {
        _geolocation = geolocation;
        _logger = logger;
    }

    public async Task<GpsFix?> ReadAsync(CancellationToken ct = default)
    {
        try
        {
            return await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                var status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
                if (status != PermissionStatus.Granted)
                    return null;

                var location = await _geolocation.GetLastKnownLocationAsync()
                    ?? await _geolocation.GetLocationAsync(
                        new GeolocationRequest(GeolocationAccuracy.Low, FixTimeout), ct);

                return location is null ? null : new GpsFix(location.Latitude, location.Longitude);
            });
        }
        catch (Exception ex)
        {
            _logger.LogInformation(ex, "GPS readout unavailable; using placeholder.");
            return null;
        }
    }
}
