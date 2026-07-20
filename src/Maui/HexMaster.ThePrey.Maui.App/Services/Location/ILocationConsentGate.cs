namespace HexMaster.ThePrey.Maui.App.Services.Location;

/// <summary>
/// One-time, app-entry consent gate for Google Play's Prominent Disclosure &amp; Consent policy: before
/// the player ever reaches the main menu, the app must show a prominent disclosure describing the
/// background-location collection used while a game is in progress and require an explicit affirmative
/// action. Once granted, consent is remembered and never re-shown across launches. Modeled on
/// <see cref="Dialogs.IConfirmationDialog"/> so it is injectable and mockable in the plain
/// <c>net10.0</c> test project; the MAUI-coupled implementation (Preferences, the disclosure dialog, the
/// iOS consent wall, and the Android/desktop app-exit) is excluded from the test build.
/// </summary>
public interface ILocationConsentGate
{
    /// <summary>
    /// Resolves once the player has consented: immediately when a prior launch already recorded
    /// consent, otherwise after the disclosure is shown and accepted. Declining is not an option the app
    /// can honor at this gate — on platforms that permit a programmatic quit (Android, Windows, Mac) the
    /// app exits instead of returning; on iOS (where a programmatic quit is disallowed) a non-dismissable
    /// consent wall blocks until the player accepts. Callers can therefore simply await this and proceed.
    /// </summary>
    Task EnsureConsentAsync(CancellationToken ct = default);
}
