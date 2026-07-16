using HexMaster.ThePrey.Maui.App.Services.Api;
using HexMaster.ThePrey.Maui.App.Services.Authentication;
using HexMaster.ThePrey.Maui.App.Services.Localization;
using HexMaster.ThePrey.Maui.App.Services.Navigation;
using Microsoft.Extensions.Logging;

namespace HexMaster.ThePrey.Maui.App.ViewModels;

/// <summary>
/// Drives the game-configuration page reached from the Start Game button: the five fixed-choice selectors
/// (each defaulted), the selected-playfield row (opened through the picker navigator), and the Create
/// action. On create it sources the caller's display name, converts the two ping minutes to seconds, sends
/// <c>POST /games</c>, maps each outcome to a distinct state, and navigates to the game route on success.
/// Plain .NET (HTTP, navigation, and localization behind interfaces) so it is fully unit-testable.
/// </summary>
public sealed class StartGameViewModel : ObservableObject
{
    public static readonly IReadOnlyList<int> DurationOptions = [30, 60, 90];
    public static readonly IReadOnlyList<int> HeadstartOptions = [5, 10, 15];
    public static readonly IReadOnlyList<int> EndgameOptions = [5, 10, 15];
    public static readonly IReadOnlyList<int> PingOptions = [2, 3, 5];
    public static readonly IReadOnlyList<int> EndgamePingOptions = [1, 2, 3, 5];

    /// <summary>Localization key for the fallback display name when the profile has none.</summary>
    public const string DefaultDisplayNameKey = "StartGame_DefaultDisplayName";

    private readonly IGameApiClient _gameApi;
    private readonly IUserApiClient _userApi;
    private readonly IAccessTokenProvider _accessTokenProvider;
    private readonly IPlayfieldSelectNavigator _playfieldSelectNavigator;
    private readonly IMenuNavigator _navigator;
    private readonly ILocalizationService _localization;
    private readonly ILogger<StartGameViewModel> _logger;

    private int _selectedDuration = 30;
    private int _selectedHeadstart = 5;
    private int _selectedEndgame = 10;
    private int _selectedPing = 2;
    private int _selectedEndgamePing = 1;

    private PlayFieldSummary? _selectedPlayfield;
    private bool _isBusy;
    private bool _hasError;
    private bool _hasValidationError;

    public StartGameViewModel(
        IGameApiClient gameApi,
        IUserApiClient userApi,
        IAccessTokenProvider accessTokenProvider,
        IPlayfieldSelectNavigator playfieldSelectNavigator,
        IMenuNavigator navigator,
        ILocalizationService localization,
        ILogger<StartGameViewModel> logger)
    {
        _gameApi = gameApi;
        _userApi = userApi;
        _accessTokenProvider = accessTokenProvider;
        _playfieldSelectNavigator = playfieldSelectNavigator;
        _navigator = navigator;
        _localization = localization;
        _logger = logger;

        SelectPlayfieldCommand = new RelayCommand(SelectPlayfieldAsync, () => !IsBusy);
        CreateCommand = new RelayCommand(CreateAsync, () => CanCreate);
    }

    public IReadOnlyList<int> DurationChoices => DurationOptions;
    public IReadOnlyList<int> HeadstartChoices => HeadstartOptions;
    public IReadOnlyList<int> EndgameChoices => EndgameOptions;
    public IReadOnlyList<int> PingChoices => PingOptions;
    public IReadOnlyList<int> EndgamePingChoices => EndgamePingOptions;

    /// <summary>Opens the playfield picker and stores the returned playfield (unchanged on dismiss).</summary>
    public RelayCommand SelectPlayfieldCommand { get; }

    /// <summary>Creates the game. Enabled only when a playfield is selected and nothing is in flight.</summary>
    public RelayCommand CreateCommand { get; }

    public int SelectedDuration
    {
        get => _selectedDuration;
        set => SetProperty(ref _selectedDuration, value);
    }

    public int SelectedHeadstart
    {
        get => _selectedHeadstart;
        set => SetProperty(ref _selectedHeadstart, value);
    }

    public int SelectedEndgame
    {
        get => _selectedEndgame;
        set => SetProperty(ref _selectedEndgame, value);
    }

    public int SelectedPing
    {
        get => _selectedPing;
        set => SetProperty(ref _selectedPing, value);
    }

    public int SelectedEndgamePing
    {
        get => _selectedEndgamePing;
        set => SetProperty(ref _selectedEndgamePing, value);
    }

    /// <summary>The chosen playfield, or null until one is picked.</summary>
    public PlayFieldSummary? SelectedPlayfield
    {
        get => _selectedPlayfield;
        private set
        {
            if (SetProperty(ref _selectedPlayfield, value))
            {
                OnPropertyChanged(nameof(SelectedPlayfieldName));
                OnPropertyChanged(nameof(HasSelectedPlayfield));
                OnPropertyChanged(nameof(HasNoSelectedPlayfield));
                OnStateChanged();
            }
        }
    }

    /// <summary>The selected playfield's name, or empty when none is selected (the page shows a prompt).</summary>
    public string SelectedPlayfieldName => SelectedPlayfield?.Name ?? string.Empty;

    /// <summary>True once a playfield has been selected (drives the name display).</summary>
    public bool HasSelectedPlayfield => SelectedPlayfield is not null;

    /// <summary>True while no playfield is selected (drives the choose-one prompt).</summary>
    public bool HasNoSelectedPlayfield => SelectedPlayfield is null;

    /// <summary>True while a create request is in flight.</summary>
    public bool IsBusy
    {
        get => _isBusy;
        private set { if (SetProperty(ref _isBusy, value)) OnStateChanged(); }
    }

    /// <summary>True when the last create attempt failed (no token, unauthorized, network, or unexpected).</summary>
    public bool HasError
    {
        get => _hasError;
        private set => SetProperty(ref _hasError, value);
    }

    /// <summary>True when the backend rejected the configuration (<c>400</c>); selections are kept.</summary>
    public bool HasValidationError
    {
        get => _hasValidationError;
        private set => SetProperty(ref _hasValidationError, value);
    }

    /// <summary>Create is enabled only when a playfield is selected and no create request is in flight.</summary>
    public bool CanCreate => SelectedPlayfield is not null && !IsBusy;

    internal async Task SelectPlayfieldAsync()
    {
        var chosen = await _playfieldSelectNavigator.SelectPlayfieldAsync();
        if (chosen is not null)
            SelectedPlayfield = chosen;
    }

    internal async Task CreateAsync()
    {
        if (!CanCreate)
            return;

        HasError = false;
        HasValidationError = false;
        IsBusy = true;
        try
        {
            var token = await _accessTokenProvider.GetAccessTokenAsync();
            if (token is null)
            {
                HasError = true;
                return;
            }

            var displayName = await ResolveDisplayNameAsync(token);
            if (displayName is null)
            {
                // Only an Unauthorized profile read blocks create (already invalidated the token below).
                HasError = true;
                return;
            }

            // Durations stay in minutes; the two ping intervals convert minutes → seconds at this boundary.
            var request = new CreateGameParameters(
                SelectedPlayfield!.Id,
                displayName,
                SelectedDuration,
                SelectedHeadstart,
                SelectedEndgame,
                SelectedPing * 60,
                SelectedEndgamePing * 60);

            var result = await _gameApi.CreateGameAsync(request, token);
            switch (result.Outcome)
            {
                case CreateGameOutcome.Success when result.Game is not null:
                    // Replace this create page with the lobby instead of stacking it: the ".." pops the
                    // create page, then the game route pushes the lobby, so Back from the lobby returns to
                    // the menu (not to the create form the game was already made from). The created game's
                    // id is handed to the lobby so it loads that game directly — a just-created game is
                    // still in its lobby phase and GET /games/active (started games only) would not find it.
                    await _navigator.GoToAsync(
                        $"../{MainMenuViewModel.GameRoute}?{GameLobbyViewModel.GameIdQueryKey}={result.Game.Id}");
                    break;
                case CreateGameOutcome.Validation:
                    HasValidationError = true;
                    break;
                case CreateGameOutcome.Unauthorized:
                    _accessTokenProvider.Invalidate();
                    HasError = true;
                    break;
                default:
                    HasError = true;
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create the game.");
            HasError = true;
        }
        finally
        {
            IsBusy = false;
        }
    }

    // Sources the display name for the create request. Success → the profile name (or the default when it
    // is blank); NotFound / Error → the default (never block create on a profile read); Unauthorized →
    // null after invalidating the token, so the caller surfaces the unauthorized error state.
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

    private string DefaultDisplayName() => _localization[DefaultDisplayNameKey];

    private void OnStateChanged()
    {
        OnPropertyChanged(nameof(CanCreate));
        SelectPlayfieldCommand.RaiseCanExecuteChanged();
        CreateCommand.RaiseCanExecuteChanged();
    }
}
