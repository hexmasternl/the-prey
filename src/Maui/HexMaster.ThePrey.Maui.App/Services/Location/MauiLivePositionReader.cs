using Microsoft.Extensions.Logging;
using Microsoft.Maui.Devices.Sensors;
using MauiLocation = Microsoft.Maui.Devices.Sensors.Location;

namespace HexMaster.ThePrey.Maui.App.Services.Location;

/// <summary>
/// Default <see cref="ILivePositionReader"/> backed by MAUI <see cref="IGeolocation"/> foreground
/// listening. Requests when-in-use permission, then streams <c>LocationChanged</c> as <see cref="GpsFix"/>
/// events on the main thread. Any permission denial, listen failure, or platform error is swallowed
/// (logged) so the gameplay map degrades to "no self fix" rather than crashing. Excluded from the
/// unit-test build (the view model is tested against a fake).
///
/// <para>This is a singleton shared by both gameplay maps, and <see cref="Start"/>/<see cref="Stop"/> are
/// fire-and-forget from page appear/disappear — a hunter/prey page swap issues Stop then Start back to
/// back. Callers therefore only set the <em>desired</em> state; a gated reconcile loop drives the actual
/// geolocation stream to match, so the two async bodies can never interleave into a subscribed-but-not-
/// listening (or listening-but-unsubscribed) reader.</para>
/// </summary>
public sealed class MauiLivePositionReader : ILivePositionReader
{
    private static readonly TimeSpan MinInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan SeedTimeout = TimeSpan.FromSeconds(10);

    private readonly IGeolocation _geolocation;
    private readonly ILogger<MauiLivePositionReader> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private volatile bool _wantListening;
    private bool _listening;

    public MauiLivePositionReader(IGeolocation geolocation, ILogger<MauiLivePositionReader> logger)
    {
        _geolocation = geolocation;
        _logger = logger;
    }

    public event Action<GpsFix>? PositionChanged;

    public void Start()
    {
        _wantListening = true;
        Reconcile();
    }

    public void Stop()
    {
        _wantListening = false;
        Reconcile();
    }

    private void Reconcile() => _ = MainThread.InvokeOnMainThreadAsync(ReconcileAsync);

    // Drives the actual stream to whatever the latest Start/Stop asked for. Serialised on _gate, and it
    // re-reads _wantListening inside, so an out-of-order pair of calls still settles on the last one.
    private async Task ReconcileAsync()
    {
        await _gate.WaitAsync();
        try
        {
            if (_wantListening)
                await StartListeningAsync();
            else
                StopListening();
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task StartListeningAsync()
    {
        if (_listening)
            return;

        try
        {
            var status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
            if (status != PermissionStatus.Granted)
            {
                _logger.LogInformation("Live position listening not started — location permission not granted.");
                return;
            }

            _geolocation.LocationChanged += OnLocationChanged;
            _geolocation.ListeningFailed += OnListeningFailed;

            // StartListeningForegroundAsync throws when a stream is already open, which happens if a
            // previous page's stop never completed. Adopt the open stream instead of blowing up.
            if (!_geolocation.IsListeningForeground)
                await _geolocation.StartListeningForegroundAsync(
                    new GeolocationListeningRequest(GeolocationAccuracy.Best, MinInterval));

            _listening = true;
        }
        catch (Exception ex)
        {
            _geolocation.LocationChanged -= OnLocationChanged;
            _geolocation.ListeningFailed -= OnListeningFailed;
            _logger.LogInformation(ex, "Live position listening unavailable.");
            return;
        }

        await SeedFirstFixAsync();
    }

    /// <summary>
    /// Publishes a fix up front. <c>LocationChanged</c> only fires when the device actually moves, so a
    /// player standing still when the map opens would otherwise have no self arrow at all until they
    /// walked far enough to trip an update.
    /// </summary>
    private async Task SeedFirstFixAsync()
    {
        try
        {
            // Cached fix first — near-instant, so the arrow appears immediately if one is available.
            if (await _geolocation.GetLastKnownLocationAsync() is { } cached)
                Publish(cached);

            // Then a real read, because the cached fix can be absent or stale after a cold start.
            if (await _geolocation.GetLocationAsync(new GeolocationRequest(GeolocationAccuracy.Best, SeedTimeout)) is { } fresh)
                Publish(fresh);
        }
        catch (Exception ex)
        {
            _logger.LogInformation(ex, "Seeding the first live position fix failed.");
        }
    }

    private void StopListening()
    {
        if (!_listening)
            return;

        _listening = false;
        _geolocation.LocationChanged -= OnLocationChanged;
        _geolocation.ListeningFailed -= OnListeningFailed;
        try { _geolocation.StopListeningForeground(); }
        catch (Exception ex) { _logger.LogInformation(ex, "Stopping live position listening failed."); }
    }

    // MAUI has already torn the stream down by the time this fires and sends no further LocationChanged.
    // Clearing _listening is what matters: without it this singleton would look "already listening"
    // forever and every later Start() would no-op, killing the self arrow for the rest of the session.
    private void OnListeningFailed(object? sender, GeolocationListeningFailedEventArgs e)
    {
        _logger.LogInformation("Live position listening failed ({Error}); re-arming on the next start.", e.Error);
        _geolocation.LocationChanged -= OnLocationChanged;
        _geolocation.ListeningFailed -= OnListeningFailed;
        _listening = false;
    }

    private void OnLocationChanged(object? sender, GeolocationLocationChangedEventArgs e) => Publish(e.Location);

    // The seed reads are awaited, so the page can have disappeared in the meantime; don't move the
    // arrow on a map that is no longer listening.
    private void Publish(MauiLocation location)
    {
        if (_wantListening)
            PositionChanged?.Invoke(new GpsFix(location.Latitude, location.Longitude));
    }
}
