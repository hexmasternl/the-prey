using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.App;

namespace HexMaster.ThePrey.Maui.App.Services.Location;

/// <summary>
/// The Android foreground service that keeps the process alive while a game is tracked. It runs as a
/// <c>location</c>-typed foreground service with a persistent, low-importance notification — the only
/// Android-sanctioned way to keep location work running with the screen off without Doze killing it.
/// The <see cref="ForegroundService"/> attribute emits the <c>&lt;service&gt;</c> manifest entry with the
/// correct JNI name. Reporting logic lives in the platform-neutral coordinator; this service only holds
/// the process up.
/// </summary>
[Service(Enabled = true, Exported = false, ForegroundServiceType = ForegroundService.TypeLocation)]
public sealed class LocationForegroundService : Service
{
    internal const string ChannelId = "theprey_location_tracking";
    private const int NotificationId = 0x7BEE;

    // User-facing notification copy. A background Android service has no clean DI/localization seam, so
    // this stays a concise English string; the notification is mandatory and cannot be hidden.
    private const string NotificationTitle = "The Prey — game in progress";
    private const string NotificationText = "Sharing your location with the game. Stops when the game ends.";

    public override IBinder? OnBind(Intent? intent) => null;

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        CreateNotificationChannel();
        var notification = BuildNotification();

        if (OperatingSystem.IsAndroidVersionAtLeast(29))
            StartForeground(NotificationId, notification, ForegroundService.TypeLocation);
        else
            StartForeground(NotificationId, notification);

        // Sticky: if the OS kills us under memory pressure, re-create the service (the game may still run).
        return StartCommandResult.Sticky;
    }

    private void CreateNotificationChannel()
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(26))
            return;

        var manager = (NotificationManager?)GetSystemService(NotificationService);
        if (manager is null || manager.GetNotificationChannel(ChannelId) is not null)
            return;

        // Low importance: no sound/heads-up — a quiet, persistent "tracking" indicator.
        var channel = new NotificationChannel(ChannelId, "Location tracking", NotificationImportance.Low)
        {
            Description = "Shows while The Prey is sharing your location during a game."
        };
        manager.CreateNotificationChannel(channel);
    }

    private Notification BuildNotification() =>
        new NotificationCompat.Builder(this, ChannelId)
            .SetContentTitle(NotificationTitle)
            .SetContentText(NotificationText)
            // A stock framework glyph — present on every API level, so no coupling to the generated
            // app-resource designer class (whose type name varies across SDK versions).
            .SetSmallIcon(global::Android.Resource.Drawable.SymDefAppIcon)
            .SetOngoing(true)
            .SetPriority((int)NotificationPriority.Low)
            .SetCategory(NotificationCompat.CategoryService)
            .Build();
}
