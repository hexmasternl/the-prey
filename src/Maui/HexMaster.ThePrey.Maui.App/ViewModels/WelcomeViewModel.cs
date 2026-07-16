using HexMaster.ThePrey.Maui.App.Services.Navigation;
using HexMaster.ThePrey.Maui.App.Services.Session;
using Microsoft.Extensions.Logging;

namespace HexMaster.ThePrey.Maui.App.ViewModels;

/// <summary>
/// Drives the welcome screen's startup bootstrap: shows progress, asks
/// <see cref="ISessionService"/> to establish a session, then routes to the post-boot destination. That
/// destination is a launch invite link when one was captured (so a cold start from an invite lands on the
/// Join page), otherwise the main menu. Re-runs every time the welcome page appears (e.g. after login).
/// </summary>
public sealed class WelcomeViewModel : ObservableObject
{
    private readonly ISessionService _session;
    private readonly IInviteDeepLinkHandler _deepLinkHandler;
    private readonly ILogger<WelcomeViewModel> _logger;

    private bool _isBusy;
    private string _statusMessage = "ESTABLISHING SIGNAL…";
    private bool _isBootstrapping;

    public WelcomeViewModel(
        ISessionService session,
        IInviteDeepLinkHandler deepLinkHandler,
        ILogger<WelcomeViewModel> logger)
    {
        _session = session;
        _deepLinkHandler = deepLinkHandler;
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

    public async Task BootstrapAsync()
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

            // Exactly one post-boot navigation, decided here so nothing races it: honor a launch invite link
            // when one was captured (land on the Join page), otherwise fall back to the main menu — which
            // reflects sign-in and active-game state through its own buttons.
            if (!await _deepLinkHandler.ReplayPendingAsync())
                await Shell.Current.GoToAsync("home");
        }
        finally
        {
            IsBusy = false;
            _isBootstrapping = false;
        }
    }
}
