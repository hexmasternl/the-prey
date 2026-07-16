using Android.Content;
using Microsoft.Extensions.Logging;
using Application = Android.App.Application;

namespace HexMaster.ThePrey.Maui.App.Services.Location;

/// <summary>
/// Android <see cref="IBackgroundExecutionHost"/>. Requests the runtime permissions required for
/// background tracking (fine location, background location, and — on API 33+ — notifications), then
/// starts/stops <see cref="LocationForegroundService"/>. Requesting a denied background permission does
/// not fail the game: the foreground service still runs while the app is visible (foreground-only
/// degradation); the persistent notification appears while the service is up and is removed on stop.
/// </summary>
public sealed class AndroidBackgroundExecutionHost : IBackgroundExecutionHost
{
    private readonly ILogger<AndroidBackgroundExecutionHost> _logger;

    public AndroidBackgroundExecutionHost(ILogger<AndroidBackgroundExecutionHost> logger) => _logger = logger;

    public async Task StartAsync(CancellationToken ct = default)
    {
        await EnsurePermissionsAsync();

        try
        {
            var context = Application.Context;
            var intent = new Intent(context, typeof(LocationForegroundService));

            if (OperatingSystem.IsAndroidVersionAtLeast(26))
                context.StartForegroundService(intent);
            else
                context.StartService(intent);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to start the Android location foreground service.");
        }
    }

    public Task StopAsync()
    {
        try
        {
            var context = Application.Context;
            context.StopService(new Intent(context, typeof(LocationForegroundService)));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to stop the Android location foreground service.");
        }

        return Task.CompletedTask;
    }

    // Requests the location authorizations on the main thread. When-in-use is requested first (required
    // for any fix), then background/always (needed for screen-off tracking). A denial is logged, not
    // thrown — the coordinator continues in foreground-only mode.
    private async Task EnsurePermissionsAsync()
    {
        try
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                var whenInUse = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
                if (whenInUse != PermissionStatus.Granted)
                {
                    _logger.LogInformation("Foreground location permission not granted — tracking will produce no fixes.");
                    return;
                }

                // POST_NOTIFICATIONS (API 33+) — the foreground-service notification needs it to show.
                if (OperatingSystem.IsAndroidVersionAtLeast(33))
                    await Permissions.RequestAsync<PostNotificationsPermission>();

                var always = await Permissions.RequestAsync<Permissions.LocationAlways>();
                if (always != PermissionStatus.Granted)
                    _logger.LogInformation("Background (always) location denied — degrading to foreground-only reporting.");
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Requesting location permissions failed.");
        }
    }

    /// <summary>Runtime request for <c>POST_NOTIFICATIONS</c> (Android 13+), which MAUI has no built-in permission for.</summary>
    private sealed class PostNotificationsPermission : Permissions.BasePlatformPermission
    {
        public override (string androidPermission, bool isRuntime)[] RequiredPermissions { get; } =
            new[] { (global::Android.Manifest.Permission.PostNotifications, true) };
    }
}
