using System.Linq;
using HexMaster.ThePrey.Maui.App.Services.Api;
using HexMaster.ThePrey.Maui.App.Services.Authentication;
using HexMaster.ThePrey.Maui.App.Services.Localization;
using HexMaster.ThePrey.Maui.App.Services.Navigation;
using HexMaster.ThePrey.Maui.App.Services.Session;
using Microsoft.Extensions.Logging;

namespace HexMaster.ThePrey.Maui.App.ViewModels;

/// <summary>
/// Drives the Join Game page reached from an invite deep link. Receives the pending game id
/// (<see cref="IQueryAttributable"/>), gates on sign-in (driving the shared interactive login while keeping
/// the id), accepts a 4-digit numeric join code, sources the caller's display name, sends the authenticated
/// <c>POST /games/{id}/join</c>, and maps each outcome to a distinct on-page state (navigating to the game
/// route on success). Plain .NET (HTTP, login, session, navigation, and localization behind interfaces) so
/// it is fully unit-testable.
/// </summary>
public sealed class JoinGameViewModel : ObservableObject
{
    /// <summary>The Shell route this page is registered under (the invite deep link routes here).</summary>
    public const string JoinRoute = "join";

    /// <summary>The exact length of the backend's <c>GameCode</c> (<c>Game.GameCodeLength = 4</c>).</summary>
    public const int CodeLength = 4;

    private readonly IGameApiClient _gameApi;
    private readonly IUserApiClient _userApi;
    private readonly IAccessTokenProvider _accessTokenProvider;
    private readonly IInteractiveLoginService _login;
    private readonly ISessionService _session;
    private readonly IMenuNavigator _navigator;
    private readonly ILocalizationService _localization;
    private readonly ILogger<JoinGameViewModel> _logger;

    private Guid? _gameId;
    private string _code = string.Empty;
    private string? _conflictCode;

    private bool _isSignedIn;
    private bool _isBusy;
    private bool _hasInvalidCode;
    private bool _hasNotFound;
    private bool _hasConflict;
    private bool _hasError;

    public JoinGameViewModel(
        IGameApiClient gameApi,
        IUserApiClient userApi,
        IAccessTokenProvider accessTokenProvider,
        IInteractiveLoginService login,
        ISessionService session,
        IMenuNavigator navigator,
        ILocalizationService localization,
        ILogger<JoinGameViewModel> logger)
    {
        _gameApi = gameApi;
        _userApi = userApi;
        _accessTokenProvider = accessTokenProvider;
        _login = login;
        _session = session;
        _navigator = navigator;
        _localization = localization;
        _logger = logger;

        LogInCommand = new RelayCommand(LogInAsync, () => !IsSignedIn && !IsBusy);
        JoinCommand = new RelayCommand(JoinAsync, () => CanJoin);
    }

    /// <summary>Drives the shared interactive login when signed out; keeps the pending game id.</summary>
    public RelayCommand LogInCommand { get; }

    /// <summary>Joins the game. Enabled only when the code is 4 digits, the user is signed in, and idle.</summary>
    public RelayCommand JoinCommand { get; }

    /// <summary>The entered join code — enforced to at most <see cref="CodeLength"/> decimal digits.</summary>
    public string Code
    {
        get => _code;
        set
        {
            var digits = new string((value ?? string.Empty).Where(char.IsDigit).Take(CodeLength).ToArray());
            if (_code != digits)
            {
                _code = digits;
                OnPropertyChanged();
                OnStateChanged();
            }
            else if (digits != value)
            {
                // Non-digits / overflow were stripped but the net code is unchanged: re-notify so a two-way
                // bound entry reverts its text to the sanitized value.
                OnPropertyChanged();
            }
        }
    }

    /// <summary>True once the code holds exactly <see cref="CodeLength"/> digits.</summary>
    public bool IsCodeComplete => _code.Length == CodeLength;

    /// <summary>True when a valid access token is available (drives the code entry vs the sign-in prompt).</summary>
    public bool IsSignedIn
    {
        get => _isSignedIn;
        private set { if (SetProperty(ref _isSignedIn, value)) OnStateChanged(); }
    }

    /// <summary>True while signed out — the page shows the sign-in prompt instead of the code entry.</summary>
    public bool IsSignedOut => !IsSignedIn;

    /// <summary>True while a login or join request is in flight.</summary>
    public bool IsBusy
    {
        get => _isBusy;
        private set { if (SetProperty(ref _isBusy, value)) OnStateChanged(); }
    }

    /// <summary>True when the last join was rejected as a wrong code (<c>400</c>); the code is kept.</summary>
    public bool HasInvalidCode
    {
        get => _hasInvalidCode;
        private set => SetProperty(ref _hasInvalidCode, value);
    }

    /// <summary>True when the game was not found (<c>404</c>).</summary>
    public bool HasNotFound
    {
        get => _hasNotFound;
        private set => SetProperty(ref _hasNotFound, value);
    }

    /// <summary>True when a game-state rule blocked the join (<c>409</c>); see <see cref="ConflictMessage"/>.</summary>
    public bool HasConflict
    {
        get => _hasConflict;
        private set { if (SetProperty(ref _hasConflict, value)) OnPropertyChanged(nameof(ConflictMessage)); }
    }

    /// <summary>True on a transient/unexpected failure (network, missing id, or unexpected status).</summary>
    public bool HasError
    {
        get => _hasError;
        private set => SetProperty(ref _hasError, value);
    }

    /// <summary>The localized conflict message chosen from the backend's stable rule <c>code</c>.</summary>
    public string ConflictMessage => _conflictCode switch
    {
        "game_already_started" => _localization["Join_Conflict_AlreadyStarted"],
        "game_full" => _localization["Join_Conflict_Full"],
        _ => _localization["Join_Conflict_Generic"],
    };

    /// <summary>Join is enabled only with a complete code, a signed-in user, and nothing in flight.</summary>
    public bool CanJoin => IsCodeComplete && IsSignedIn && !IsBusy;

    /// <summary>
    /// Sets the pending game id to join, supplied by the page from the <c>gameId</c> navigation query (the
    /// invite deep link carries it). Held across the sign-in round-trip so a signed-out recipient never loses
    /// the invite. Kept as a plain method (the page owns the MAUI <c>IQueryAttributable</c> plumbing) so the
    /// view model stays free of MAUI types and fully unit-testable.
    /// </summary>
    public void SetPendingGame(Guid? gameId) => _gameId = gameId;

    /// <summary>
    /// Called when the page appears: resolves the sign-in state (retaining the pending game id). A recipient
    /// landing here from an invite link MUST be signed in, so when signed out this drives the interactive
    /// login straight away rather than waiting on a tap; on return (success) the code entry is shown, and on
    /// cancel/failure the sign-in prompt remains as a manual retry.
    /// </summary>
    public async Task OnAppearingAsync()
    {
        await RefreshSignInStateAsync();

        if (IsSignedOut && !IsBusy)
            await LogInAsync();
    }

    private async Task RefreshSignInStateAsync()
    {
        try
        {
            var token = await _accessTokenProvider.GetAccessTokenAsync();
            IsSignedIn = token is not null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to resolve the join-page sign-in state; treating as signed out.");
            IsSignedIn = false;
        }
    }

    internal async Task LogInAsync()
    {
        if (IsSignedIn || IsBusy)
            return;

        ClearMessages();
        IsBusy = true;
        try
        {
            var outcome = await _login.LoginAsync();
            if (outcome == InteractiveLoginOutcome.Success)
            {
                await _session.TryEstablishSessionAsync();
                await RefreshSignInStateAsync();
            }
            // Cancelled / Failed / NoRefreshToken: stay signed out (the pending game id is retained) and
            // let the user retry.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Interactive login from the join page failed.");
        }
        finally
        {
            IsBusy = false;
        }
    }

    internal async Task JoinAsync()
    {
        if (!CanJoin)
            return;

        ClearMessages();
        IsBusy = true;
        try
        {
            var token = await _accessTokenProvider.GetAccessTokenAsync();
            if (token is null)
            {
                SetSignedOut();
                return;
            }

            var displayName = await ResolveDisplayNameAsync(token);
            if (displayName is null)
            {
                // Only an Unauthorized profile read returns null (the token was already invalidated below).
                SetSignedOut();
                return;
            }

            if (_gameId is not Guid id)
            {
                HasError = true;
                return;
            }

            var result = await _gameApi.JoinGameAsync(id, _code, displayName, token);
            switch (result.Outcome)
            {
                case JoinGameOutcome.Success when result.Game is not null:
                    // Replace this join page with the game/lobby ("../" pops the join page first) and hand it
                    // the joined game's id so it loads that game directly — matching the create flow.
                    await _navigator.GoToAsync(
                        $"../{MainMenuViewModel.GameRoute}?{GameLobbyViewModel.GameIdQueryKey}={result.Game.Id}");
                    break;
                case JoinGameOutcome.InvalidCode:
                    HasInvalidCode = true; // keep the entered code so the user can correct it
                    break;
                case JoinGameOutcome.NotFound:
                    HasNotFound = true;
                    break;
                case JoinGameOutcome.Conflict:
                    _conflictCode = result.Code;
                    HasConflict = true;
                    break;
                case JoinGameOutcome.Unauthorized:
                    _accessTokenProvider.Invalidate();
                    SetSignedOut();
                    break;
                default:
                    HasError = true;
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to join the game.");
            HasError = true;
        }
        finally
        {
            IsBusy = false;
        }
    }

    // Sources the display name for the join request. Success → the profile name (or the default when blank);
    // NotFound / Error → the default (never block the join on a profile read); Unauthorized → null after
    // invalidating the token, so the caller falls back to the signed-out state.
    private async Task<string?> ResolveDisplayNameAsync(string token)
    {
        try
        {
            var result = await _userApi.GetCurrentUserAsync(token);
            switch (result.Outcome)
            {
                case UserSettingsOutcome.Success:
                    var name = result.Settings?.DisplayName;
                    return string.IsNullOrWhiteSpace(name) ? DefaultDisplayName() : name;
                case UserSettingsOutcome.Unauthorized:
                    _accessTokenProvider.Invalidate();
                    return null;
                default: // NotFound / Error
                    return DefaultDisplayName();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read the current user's display name; using the default.");
            return DefaultDisplayName();
        }
    }

    private string DefaultDisplayName() => _localization[StartGameViewModel.DefaultDisplayNameKey];

    private void SetSignedOut() => IsSignedIn = false;

    private void ClearMessages()
    {
        HasInvalidCode = false;
        HasNotFound = false;
        HasConflict = false;
        HasError = false;
    }

    private void OnStateChanged()
    {
        OnPropertyChanged(nameof(IsCodeComplete));
        OnPropertyChanged(nameof(IsSignedOut));
        OnPropertyChanged(nameof(CanJoin));

        LogInCommand.RaiseCanExecuteChanged();
        JoinCommand.RaiseCanExecuteChanged();
    }
}
