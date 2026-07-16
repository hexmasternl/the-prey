using HexMaster.ThePrey.Maui.App.Services.Session;
using Microsoft.Extensions.Logging;

namespace HexMaster.ThePrey.Maui.App.ViewModels;

/// <summary>
/// Drives the welcome screen's startup bootstrap: shows progress, asks
/// <see cref="ISessionService"/> to establish a session, then routes to the game, home, or
/// login destination. Re-runs every time the welcome page appears (e.g. after login).
/// </summary>
public sealed class WelcomeViewModel : ObservableObject
{
    private readonly ISessionService _session;
    private readonly ILogger<WelcomeViewModel> _logger;

    private bool _isBusy;
    private string _statusMessage = "ESTABLISHING SIGNAL…";
    private bool _isBootstrapping;

    public WelcomeViewModel(ISessionService session, ILogger<WelcomeViewModel> logger)
    {
        _session = session;
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
            var result = await _session.TryEstablishSessionAsync();

            StatusMessage = result.Outcome switch
            {
                SessionOutcome.ActiveGame => "OPERATION ACTIVE",
                SessionOutcome.NoActiveGame => "STANDING BY",
                _ => "AUTHENTICATION REQUIRED"
            };

            // The main menu is the universal post-boot destination for every outcome; it reflects
            // sign-in and active-game state through its own buttons.
            await Shell.Current.GoToAsync("home");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Startup bootstrap failed; routing to the main menu in its signed-out state.");
            StatusMessage = "AUTHENTICATION REQUIRED";
            await Shell.Current.GoToAsync("home");
        }
        finally
        {
            IsBusy = false;
            _isBootstrapping = false;
        }
    }
}
