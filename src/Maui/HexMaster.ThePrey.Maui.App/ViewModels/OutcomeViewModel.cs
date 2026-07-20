using System.Globalization;
using System.Windows.Input;
using HexMaster.ThePrey.Maui.App.Services.Api;
using HexMaster.ThePrey.Maui.App.Services.Authentication;
using HexMaster.ThePrey.Maui.App.Services.Localization;
using HexMaster.ThePrey.Maui.App.Services.Navigation;
using HexMaster.ThePrey.Maui.App.Services.Session;
using Microsoft.Extensions.Logging;

namespace HexMaster.ThePrey.Maui.App.ViewModels;

/// <summary>
/// Drives the full-screen post-game outcome page. Reads the finished game's record via
/// <c>GET /games/{id}</c> — the authoritative post-end source, since the in-progress status endpoint no
/// longer serves a completed game — resolves the local player's win/lose through the pure
/// <see cref="GameOutcomeResolver"/>, and projects it to localized headline, winning-side, reason and
/// survivor text. A failed read degrades to a neutral "game over" state that still closes cleanly, so the
/// player is never trapped. All HTTP, identity, navigation and strings sit behind interfaces, so the whole
/// view model is unit-testable.
/// </summary>
public sealed class OutcomeViewModel : ObservableObject
{
    private readonly IGameApiClient _gameApi;
    private readonly IAccessTokenProvider _accessTokenProvider;
    private readonly ICurrentUserProvider _currentUser;
    private readonly IOutcomeNavigator _navigator;
    private readonly ILocalizationService _localization;
    private readonly ILogger<OutcomeViewModel> _logger;

    private Guid _gameId;
    private bool _isHunterHint;
    private bool _loaded;

    private bool _isBusy = true;
    private bool _isResolved;
    private bool _localPlayerWon;
    private string _headlineText = string.Empty;
    private string _winningSideText = string.Empty;
    private string _reasonText = string.Empty;
    private string _survivorsText = string.Empty;
    private bool _hasSurvivors;

    public OutcomeViewModel(
        IGameApiClient gameApi,
        IAccessTokenProvider accessTokenProvider,
        ICurrentUserProvider currentUser,
        IOutcomeNavigator navigator,
        ILocalizationService localization,
        ILogger<OutcomeViewModel> logger)
    {
        _gameApi = gameApi;
        _accessTokenProvider = accessTokenProvider;
        _currentUser = currentUser;
        _navigator = navigator;
        _localization = localization;
        _logger = logger;

        CloseCommand = new RelayCommand(() => _navigator.ReturnToMenuAsync());
    }

    /// <summary>Returns the player to the main menu, clearing the finished game from the back stack.</summary>
    public ICommand CloseCommand { get; }

    /// <summary>True while the final record is being read; the page shows its progress state.</summary>
    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(HasResult));
                OnPropertyChanged(nameof(ShowsNeutral));
            }
        }
    }

    /// <summary>The inverse of <see cref="IsBusy"/> — the result block replaces the debriefing spinner.</summary>
    public bool HasResult => !_isBusy;

    /// <summary>
    /// True once a real result was derived. False means the neutral fallback: a plain "game over" with no
    /// win/lose claim, because the record could not be read.
    /// </summary>
    public bool IsResolved
    {
        get => _isResolved;
        private set
        {
            if (SetProperty(ref _isResolved, value))
            {
                OnPropertyChanged(nameof(LocalPlayerLost));
                OnPropertyChanged(nameof(ShowsVictory));
                OnPropertyChanged(nameof(ShowsNeutral));
            }
        }
    }

    /// <summary>True when the local player won — selects the celebratory treatment.</summary>
    public bool LocalPlayerWon
    {
        get => _localPlayerWon;
        private set
        {
            if (SetProperty(ref _localPlayerWon, value))
            {
                OnPropertyChanged(nameof(LocalPlayerLost));
                OnPropertyChanged(nameof(ShowsVictory));
            }
        }
    }

    /// <summary>True when a result was resolved and it is a loss — selects the consolation treatment.</summary>
    public bool LocalPlayerLost => _isResolved && !_localPlayerWon;

    /// <summary>True when a result was resolved and it is a win. The page's victory-only emphasis binds here.</summary>
    public bool ShowsVictory => _isResolved && _localPlayerWon;

    /// <summary>
    /// True for the neutral fallback: the read finished but produced no result, so the page shows a plain
    /// "game over" with no win/lose claim — and still its close action.
    /// </summary>
    public bool ShowsNeutral => !_isBusy && !_isResolved;

    /// <summary>VICTORY / DEFEAT, or the neutral GAME OVER when the result could not be resolved.</summary>
    public string HeadlineText
    {
        get => _headlineText;
        private set => SetProperty(ref _headlineText, value);
    }

    /// <summary>Names the winning side ("the hunter wins" / "the prey win"); empty in the neutral state.</summary>
    public string WinningSideText
    {
        get => _winningSideText;
        private set => SetProperty(ref _winningSideText, value);
    }

    /// <summary>Why the game ended — every prey caught, or the clock ran out.</summary>
    public string ReasonText
    {
        get => _reasonText;
        private set => SetProperty(ref _reasonText, value);
    }

    /// <summary>
    /// How many preys made it out, shown to both sides for context whenever the preys took it.
    /// </summary>
    public string SurvivorsText
    {
        get => _survivorsText;
        private set => SetProperty(ref _survivorsText, value);
    }

    /// <summary>Whether <see cref="SurvivorsText"/> has anything to show.</summary>
    public bool HasSurvivors
    {
        get => _hasSurvivors;
        private set => SetProperty(ref _hasSurvivors, value);
    }

    /// <summary>
    /// Supplies the finished game and the caller's belief about the local player's role, before
    /// <see cref="LoadAsync"/>. The role is only a hint: the record's <c>HunterUserId</c> wins when present.
    /// </summary>
    public void Initialize(Guid gameId, bool isHunter)
    {
        _gameId = gameId;
        _isHunterHint = isHunter;
        _loaded = false;
    }

    /// <summary>
    /// Reads the completed record and resolves the outcome. Runs at most once per
    /// <see cref="Initialize"/>, so a re-appearing page does not re-fetch.
    /// </summary>
    public async Task LoadAsync(CancellationToken ct = default)
    {
        if (_loaded)
            return;
        _loaded = true;

        IsBusy = true;
        try
        {
            var token = await _accessTokenProvider.GetAccessTokenAsync(ct);
            if (token is null)
            {
                _logger.LogWarning("Outcome page could not acquire a token; showing the neutral state.");
                ApplyNeutral();
                return;
            }

            var result = await _gameApi.GetGameAsync(_gameId, token, ct);
            if (result.Outcome != GetGameOutcome.Success || result.Game is null)
            {
                _logger.LogWarning("Outcome page could not read the finished game ({Outcome}); showing the neutral state.", result.Outcome);
                ApplyNeutral();
                return;
            }

            var localUserId = await _currentUser.GetUserIdAsync(ct);
            Apply(GameOutcomeResolver.Resolve(result.Game, localUserId, _isHunterHint));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Outcome resolution failed; showing the neutral state.");
            ApplyNeutral();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void Apply(GameOutcome outcome)
    {
        LocalPlayerWon = outcome.LocalPlayerWon;
        IsResolved = true;

        HeadlineText = _localization[outcome.LocalPlayerWon ? "Outcome_Headline_Victory" : "Outcome_Headline_Defeat"];
        WinningSideText = _localization[outcome.WinningSide == OutcomeSide.Hunter
            ? "Outcome_WinningSide_Hunter"
            : "Outcome_WinningSide_Preys"];
        ReasonText = _localization[outcome.EndReason == OutcomeReason.AllPreysCaught
            ? "Outcome_Reason_AllCaught"
            : "Outcome_Reason_TimeExpired"];

        // The escapee count is context for both sides, so it accompanies every preys victory — the losing
        // hunter sees how many got away just as the winning preys see how many made it.
        HasSurvivors = outcome.WinningSide == OutcomeSide.Preys && outcome.SurvivingPreyCount > 0;
        SurvivorsText = HasSurvivors
            ? string.Format(
                CultureInfo.CurrentCulture,
                _localization[outcome.SurvivingPreyCount == 1 ? "Outcome_Survivors_One" : "Outcome_Survivors_Many"],
                outcome.SurvivingPreyCount)
            : string.Empty;
    }

    // The record could not be read. Say only what is certainly true — the game is over — and leave the
    // close action available so the player can always get back to the menu.
    private void ApplyNeutral()
    {
        IsResolved = false;
        LocalPlayerWon = false;
        HeadlineText = _localization["Outcome_Headline_Neutral"];
        WinningSideText = string.Empty;
        ReasonText = _localization["Outcome_Neutral_Message"];
        SurvivorsText = string.Empty;
        HasSurvivors = false;
    }
}
