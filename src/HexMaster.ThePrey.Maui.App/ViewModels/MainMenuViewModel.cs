using HexMaster.ThePrey.Maui.App.Services;
using HexMaster.ThePrey.Maui.App.Services.Authentication;
using HexMaster.ThePrey.Maui.App.Services.Location;
using HexMaster.ThePrey.Maui.App.Services.Navigation;
using HexMaster.ThePrey.Maui.App.Services.Platform;
using HexMaster.ThePrey.Maui.App.Services.Session;
using Microsoft.Extensions.Logging;

namespace HexMaster.ThePrey.Maui.App.ViewModels;

/// <summary>
/// Drives the main menu. Resolves sign-in + active-game state via <see cref="ISessionService"/> and
/// exposes it as button visibility/enablement flags and commands. Reuses the shared interactive
/// login flow, clears the session on log-out, and surfaces a decorative GPS + version readout.
/// Plain .NET (MAUI concerns are behind interfaces) so it is fully unit-testable.
/// </summary>
public sealed class MainMenuViewModel : ObservableObject
{
    public const string GameRoute = "game";
    public const string StartGameRoute = "start-game";
    public const string PlayfieldsRoute = "playfields";
    public const string SettingsRoute = "settings";

    private readonly ISessionService _session;
    private readonly ITokenStore _tokenStore;
    private readonly IInteractiveLoginService _login;
    private readonly IMenuNavigator _navigator;
    private readonly IApplicationExit _app;
    private readonly IGpsReader _gpsReader;
    private readonly IAppVersionProvider _versionProvider;
    private readonly ILogger<MainMenuViewModel> _logger;

    private bool _isBusy;
    private bool _isSignedIn;
    private bool _hasActiveGame;
    private string _gpsReadout = GpsCoordinateFormatter.Placeholder;

    public MainMenuViewModel(
        ISessionService session,
        ITokenStore tokenStore,
        IInteractiveLoginService login,
        IMenuNavigator navigator,
        IApplicationExit app,
        IGpsReader gpsReader,
        IAppVersionProvider versionProvider,
        ILogger<MainMenuViewModel> logger)
    {
        _session = session;
        _tokenStore = tokenStore;
        _login = login;
        _navigator = navigator;
        _app = app;
        _gpsReader = gpsReader;
        _versionProvider = versionProvider;
        _logger = logger;

        LogInCommand = new RelayCommand(LogInAsync, () => !IsSignedIn && !IsBusy);
        ResumeGameCommand = new RelayCommand(
            () => _navigator.GoToAsync(GameRoute), () => IsSignedIn && HasActiveGame && !IsBusy);
        StartGameCommand = new RelayCommand(
            () => _navigator.GoToAsync(StartGameRoute), () => IsSignedIn && !HasActiveGame && !IsBusy);
        PlayfieldsCommand = new RelayCommand(
            () => _navigator.GoToAsync(PlayfieldsRoute), () => CanUseSignedInActions);
        SettingsCommand = new RelayCommand(
            () => _navigator.GoToAsync(SettingsRoute), () => CanUseSignedInActions);
        LogOutCommand = new RelayCommand(LogOutAsync, () => CanUseSignedInActions);
        ExitCommand = new RelayCommand(() => { _app.Exit(); return Task.CompletedTask; });
    }

    public RelayCommand LogInCommand { get; }
    public RelayCommand ResumeGameCommand { get; }
    public RelayCommand StartGameCommand { get; }
    public RelayCommand PlayfieldsCommand { get; }
    public RelayCommand SettingsCommand { get; }
    public RelayCommand LogOutCommand { get; }
    public RelayCommand ExitCommand { get; }

    public bool IsBusy
    {
        get => _isBusy;
        private set { if (SetProperty(ref _isBusy, value)) OnStateChanged(); }
    }

    public bool IsSignedIn
    {
        get => _isSignedIn;
        private set { if (SetProperty(ref _isSignedIn, value)) OnStateChanged(); }
    }

    public bool HasActiveGame
    {
        get => _hasActiveGame;
        private set { if (SetProperty(ref _hasActiveGame, value)) OnStateChanged(); }
    }

    public string GpsReadout
    {
        get => _gpsReadout;
        private set => SetProperty(ref _gpsReadout, value);
    }

    public string FieldManualVersion => $"OPERATIONAL FIELD MANUAL — V {_versionProvider.Version}";

    // Log In shows only when signed out; exactly one of Resume/Start shows when signed in.
    public bool ShowLogIn => !IsSignedIn;
    public bool ShowResume => IsSignedIn && HasActiveGame;
    public bool ShowStart => IsSignedIn && !HasActiveGame;
    public bool CanUseSignedInActions => IsSignedIn && !IsBusy;

    // Exit is hidden on platforms that do not permit a programmatic quit (iOS).
    public bool ShowExit => _app.IsExitSupported;

    /// <summary>
    /// Resolves the session on appearing and maps it to menu state. While the check is in flight
    /// the menu is busy (gameplay entry stays disabled); the GPS readout loads separately so it
    /// never blocks the menu.
    /// </summary>
    public async Task LoadStateAsync()
    {
        IsBusy = true;
        try
        {
            var result = await _session.TryEstablishSessionAsync();
            ApplyOutcome(result.Outcome);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to resolve menu session state; treating as signed out.");
            ApplyOutcome(SessionOutcome.Unauthenticated);
        }
        finally
        {
            IsBusy = false;
        }

        _ = RefreshGpsAsync();
    }

    /// <summary>Fetches the decorative GPS readout; failures fall back to the placeholder.</summary>
    public async Task RefreshGpsAsync()
    {
        var fix = await _gpsReader.ReadAsync();
        GpsReadout = GpsCoordinateFormatter.Format(fix);
    }

    private async Task LogInAsync()
    {
        IsBusy = true;
        try
        {
            var outcome = await _login.LoginAsync();
            if (outcome == InteractiveLoginOutcome.Success)
            {
                var result = await _session.TryEstablishSessionAsync();
                ApplyOutcome(result.Outcome);
            }
            // Cancelled / Failed / NoRefreshToken: stay in the signed-out state and let the user retry.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Interactive login from the menu failed.");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private Task LogOutAsync()
    {
        // Clear the local session only: drop the stored refresh token (the in-memory access token
        // is never held here) and return the menu to its signed-out state.
        _tokenStore.ClearRefreshToken();
        ApplyOutcome(SessionOutcome.Unauthenticated);
        return Task.CompletedTask;
    }

    private void ApplyOutcome(SessionOutcome outcome)
    {
        switch (outcome)
        {
            case SessionOutcome.ActiveGame:
                IsSignedIn = true;
                HasActiveGame = true;
                break;
            case SessionOutcome.NoActiveGame:
                IsSignedIn = true;
                HasActiveGame = false;
                break;
            default:
                IsSignedIn = false;
                HasActiveGame = false;
                break;
        }
    }

    private void OnStateChanged()
    {
        OnPropertyChanged(nameof(ShowLogIn));
        OnPropertyChanged(nameof(ShowResume));
        OnPropertyChanged(nameof(ShowStart));
        OnPropertyChanged(nameof(CanUseSignedInActions));

        LogInCommand.RaiseCanExecuteChanged();
        ResumeGameCommand.RaiseCanExecuteChanged();
        StartGameCommand.RaiseCanExecuteChanged();
        PlayfieldsCommand.RaiseCanExecuteChanged();
        SettingsCommand.RaiseCanExecuteChanged();
        LogOutCommand.RaiseCanExecuteChanged();
    }
}
