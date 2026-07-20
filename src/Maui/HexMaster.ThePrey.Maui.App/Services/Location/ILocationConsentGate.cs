namespace HexMaster.ThePrey.Maui.App.Services.Location;

/// <summary>
/// Gates background location tracking behind Google Play's Prominent Disclosure &amp; Consent policy: a
/// prominent in-app disclosure must be shown and explicitly accepted before the OS location-permission
/// prompt for background tracking is ever triggered. Modeled on
/// <see cref="Dialogs.IConfirmationDialog"/> so it is injectable and mockable in the plain
/// <c>net10.0</c> test project; the MAUI-coupled implementation (Preferences + Permissions) is excluded
/// from the test build.
/// </summary>
public interface ILocationConsentGate
{
    /// <summary>
    /// Ensures the player has consented to background location collection, showing the prominent
    /// disclosure first when needed (no prior consent, or the OS permission is no longer granted).
    /// Returns <c>true</c> when tracking may proceed to the OS permission request; <c>false</c> when the
    /// player declined — the caller must not request OS permission or start tracking.
    /// </summary>
    Task<bool> EnsureConsentAsync(CancellationToken ct = default);
}
