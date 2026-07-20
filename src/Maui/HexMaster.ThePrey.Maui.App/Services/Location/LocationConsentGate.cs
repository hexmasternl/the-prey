using HexMaster.ThePrey.Maui.App.Services.Dialogs;
using HexMaster.ThePrey.Maui.App.Services.Localization;

namespace HexMaster.ThePrey.Maui.App.Services.Location;

/// <summary>
/// Default <see cref="ILocationConsentGate"/>. Persists the "disclosure accepted" flag with
/// <see cref="IPreferences"/> and re-checks the OS <see cref="Permissions.LocationAlways"/> status on
/// every call — a revocation in system settings must re-trigger the disclosure even when the flag is
/// still set. Reuses <see cref="IConfirmationDialog"/> for the disclosure surface itself, with copy
/// drawn from the localized <c>LocationConsent_*</c> resource keys.
/// </summary>
public sealed class LocationConsentGate : ILocationConsentGate
{
    private const string AcceptedPreferenceKey = "LocationConsent.Accepted";

    private readonly IConfirmationDialog _dialog;
    private readonly ILocalizationService _localization;
    private readonly IPreferences _preferences;

    public LocationConsentGate(IConfirmationDialog dialog, ILocalizationService localization, IPreferences preferences)
    {
        _dialog = dialog;
        _localization = localization;
        _preferences = preferences;
    }

    public async Task<bool> EnsureConsentAsync(CancellationToken ct = default)
    {
        var hasAcceptedConsent = _preferences.Get(AcceptedPreferenceKey, false);
        var permissionGranted = await Permissions.CheckStatusAsync<Permissions.LocationAlways>() == PermissionStatus.Granted;

        if (!LocationConsentPolicy.ShouldShowDisclosure(hasAcceptedConsent, permissionGranted))
            return true;

        var accepted = await _dialog.ConfirmAsync(
            _localization["LocationConsent_Title"],
            _localization["LocationConsent_Body"],
            _localization["LocationConsent_Allow"],
            _localization["LocationConsent_Decline"]);

        if (accepted)
            _preferences.Set(AcceptedPreferenceKey, true);

        return accepted;
    }
}
