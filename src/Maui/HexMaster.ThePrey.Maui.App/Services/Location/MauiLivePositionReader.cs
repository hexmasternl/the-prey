using Microsoft.Extensions.Logging;
using Microsoft.Maui.Devices.Sensors;

namespace HexMaster.ThePrey.Maui.App.Services.Location;

/// <summary>
/// Default <see cref="ILivePositionReader"/> backed by MAUI <see cref="IGeolocation"/> foreground
/// listening. Requests when-in-use permission, then streams <c>LocationChanged</c> as <see cref="GpsFix"/>
/// events on the main thread. Any permission denial, listen failure, or platform error is swallowed
/// (logged) so the gameplay map degrades to "no self fix" rather than crashing. Excluded from the
/// unit-test build (the view model is tested against a fake).
/// </summary>
public sealed class MauiLivePositionReader : ILivePositionReader
{
    private static readonly TimeSpan MinInterval = TimeSpan.FromSeconds(1);

    private readonly IGeolocation _geolocation;
    private readonly ILogger<MauiLivePositionReader> _logger;
    private bool _listening;

    public MauiLivePositionReader(IGeolocation geolocation, ILogger<MauiLivePositionReader> logger)
    {
        _geolocation = geolocation;
        _logger = logger;
    }

    public event Action<GpsFix>? PositionChanged;

    public void Start()
    {
        if (_listening)
            return;
        _listening = true;
        _ = MainThread.InvokeOnMainThreadAsync(StartListeningAsync);
    }

    public void Stop()
    {
        if (!_listening)
            return;
        _listening = false;
        _ = MainThread.InvokeOnMainThreadAsync(() =>
        {
            _geolocation.LocationChanged -= OnLocationChanged;
            try { _geolocation.StopListeningForeground(); }
            catch (Exception ex) { _logger.LogInformation(ex, "Stopping live position listening failed."); }
        });
    }

    private async Task StartListeningAsync()
    {
        try
        {
            var status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
            if (status != PermissionStatus.Granted)
            {
                _logger.LogInformation("Live position listening not started — location permission not granted.");
                return;
            }

            _geolocation.LocationChanged += OnLocationChanged;
            await _geolocation.StartListeningForegroundAsync(
                new GeolocationListeningRequest(GeolocationAccuracy.Best, MinInterval));
        }
        catch (Exception ex)
        {
            _logger.LogInformation(ex, "Live position listening unavailable.");
        }
    }

    private void OnLocationChanged(object? sender, GeolocationLocationChangedEventArgs e) =>
        PositionChanged?.Invoke(new GpsFix(e.Location.Latitude, e.Location.Longitude));
}
