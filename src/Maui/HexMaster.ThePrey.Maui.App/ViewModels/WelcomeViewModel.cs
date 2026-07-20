using HexMaster.ThePrey.Maui.App.Services.Location;
using HexMaster.ThePrey.Maui.App.Services.Navigation;
using HexMaster.ThePrey.Maui.App.Services.Session;
using Microsoft.Extensions.Logging;

namespace HexMaster.ThePrey.Maui.App.ViewModels;

/// <summary>
/// Drives the welcome screen's startup bootstrap: shows progress, asks
/// <see cref="ISessionService"/> to establish a session, then — as the one-time, app-entry gate required
/// by Google Play's Prominent Disclosure &amp; Consent policy — awaits <see cref="ILocationConsentGate"/>
/// before routing to the post-boot destination. That destination is a launch invite link when one was
/// captured (so a cold start from an invite lands on the Join page), otherwise the main menu. Re-runs
/// every time the welcome page appears (e.g. after login).
/// </summary>
public sealed class WelcomeViewModel : ObservableObject
{
    private readonly ISessionService _session;
    private readonly ILocationConsentGate _consentGate;
    private readonly IInviteDeepLinkHandler _deepLinkHandler;
    private readonly IMenuNavigator _navigator;
    private readonly ILogger<WelcomeViewModel> _logger;

    private bool _isBusy;
    private string _statusMessage = "ESTABLISHING SIGNAL…";
    private bool _isBootstrapping;

    public WelcomeViewModel(
        ISessionService session,
        ILocationConsentGate consentGate,
        IInviteDeepLinkHandler deepLinkHandler,
        IMenuNavigator navigator,
        ILogger<WelcomeViewModel> logger)
    {
        _session = session;
        _consentGate = consentGate;
        _deepLinkHandler = deepLinkHandler;
        _navigator = navigator;
        _logger = logger;
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public async Task BootstrapAsync(CancellationToken ct = default)
    {
        if (_isBootstrapping)
            return;

        _isBootstrapping = true;
        IsBusy = true;
        StatusMessage = "ESTABLISHING SIGNAL…";

        try
        {
            try
            {
                var result = await _session.TryEstablishSessionAsync();

                StatusMessage = result.Outcome switch
                {
                    SessionOutcome.ActiveGame => "OPERATION ACTIVE",
                    SessionOutcome.NoActiveGame => "STANDING BY",
                    _ => "AUTHENTICATION REQUIRED"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Startup bootstrap failed; routing to the post-boot destination signed out.");
                StatusMessage = "AUTHENTICATION REQUIRED";
            }

            // One-time, app-entry consent gate (Google Play policy): resolves immediately once consent
            // has been recorded on a prior launch, otherwise blocks here until the player accepts — no
            // post-boot navigation happens before this completes.
            await _consentGate.EnsureConsentAsync(ct);

            // Exactly one post-boot navigation, decided here so nothing races it: honor a launch invite link
            // when one was captured (land on the Join page), otherwise fall back to the main menu — which
            // reflects sign-in and active-game state through its own buttons.
            if (!await _deepLinkHandler.ReplayPendingAsync())
                await _navigator.GoToAsync("home");
        }
        finally
        {
            IsBusy = false;
            _isBootstrapping = false;
        }
    }
}
