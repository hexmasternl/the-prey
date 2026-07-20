namespace HexMaster.ThePrey.Maui.App.Services.Location;

/// <summary>
/// Pure show-or-skip decision for <see cref="LocationConsentGate"/>: whether the prominent disclosure
/// must be shown before background tracking may proceed. Deliberately free of any MAUI Essentials type
/// (Preferences, Permissions) so it can be exercised directly in the plain <c>net10.0</c> test project,
/// even though <see cref="LocationConsentGate"/> itself is platform-coupled and excluded from the test
/// build.
/// </summary>
internal static class LocationConsentPolicy
{
    /// <summary>
    /// The disclosure must be shown unless the player has previously accepted it AND the OS location
    /// permission is still granted. A revocation in system settings (permission granted but consent flag
    /// stale, or vice versa) re-triggers the disclosure rather than trusting either signal alone.
    /// </summary>
    internal static bool ShouldShowDisclosure(bool hasAcceptedConsent, bool permissionGranted) =>
        !hasAcceptedConsent || !permissionGranted;
}
