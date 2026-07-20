using HexMaster.ThePrey.Maui.App.Pages;
using HexMaster.ThePrey.Maui.App.Services.Dialogs;
using HexMaster.ThePrey.Maui.App.Services.Localization;
using HexMaster.ThePrey.Maui.App.Services.Platform;

namespace HexMaster.ThePrey.Maui.App.Services.Location;

/// <summary>
/// Default <see cref="ILocationConsentGate"/>. Persists the "consent accepted" flag with
/// <see cref="IPreferences"/> so, once granted, the disclosure is never shown again across launches.
/// Reuses <see cref="IConfirmationDialog"/> for the disclosure itself. Declining is not honored: on
/// platforms that permit a programmatic quit (<see cref="IApplicationExit"/> — Android, Windows, Mac)
/// the app exits; on iOS a full-screen, non-dismissable <see cref="LocationConsentWallPage"/> blocks
/// until the player taps Accept. Copy is drawn from the localized <c>LocationConsent_*</c> keys.
/// </summary>
public sealed class LocationConsentGate : ILocationConsentGate
{
    private const string AcceptedPreferenceKey = "LocationConsent.Accepted";

    private readonly IConfirmationDialog _dialog;
    private readonly ILocalizationService _localization;
    private readonly IPreferences _preferences;
    private readonly IApplicationExit _applicationExit;

    public LocationConsentGate(
        IConfirmationDialog dialog,
        ILocalizationService localization,
        IPreferences preferences,
        IApplicationExit applicationExit)
    {
        _dialog = dialog;
        _localization = localization;
        _preferences = preferences;
        _applicationExit = applicationExit;
    }

    public async Task EnsureConsentAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (_preferences.Get(AcceptedPreferenceKey, false))
            return; // A prior launch already recorded consent — never re-shown.

        while (true)
        {
            var accepted = await _dialog.ConfirmAsync(
                _localization["LocationConsent_Title"],
                _localization["LocationConsent_Body"],
                _localization["LocationConsent_Allow"],
                _localization["LocationConsent_Decline"]);

            if (accepted)
            {
                _preferences.Set(AcceptedPreferenceKey, true);
                return;
            }

            if (_applicationExit.IsExitSupported)
            {
                // Android/Windows/Mac: consent is mandatory to use the app at all — quit rather than
                // let the player proceed without it.
                _applicationExit.Exit();
                continue; // Defensive: re-show if Exit() hasn't torn the process down synchronously.
            }

            // iOS: a programmatic quit is disallowed by Apple's review guidelines, so block behind a
            // full-screen wall with no decline path; it resolves only when the player accepts.
            await ShowConsentWallAsync();
            _preferences.Set(AcceptedPreferenceKey, true);
            return;
        }
    }

    private static Task ShowConsentWallAsync() =>
        MainThread.InvokeOnMainThreadAsync(async () =>
        {
            var navigation = Shell.Current?.Navigation
                ?? Application.Current?.Windows.FirstOrDefault()?.Page?.Navigation;
            if (navigation is null)
                return; // No page host yet — cannot happen in practice: this only runs from
                        // WelcomeViewModel.BootstrapAsync, called once WelcomePage is already on screen.

            var completion = new TaskCompletionSource();
            await navigation.PushModalAsync(new LocationConsentWallPage(completion));
            await completion.Task;
        });
}
