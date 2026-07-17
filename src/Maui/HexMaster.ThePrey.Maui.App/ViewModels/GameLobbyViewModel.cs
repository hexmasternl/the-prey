using System.Collections.ObjectModel;
using HexMaster.ThePrey.Maui.App.Configuration;
using HexMaster.ThePrey.Maui.App.Services.Api;
using HexMaster.ThePrey.Maui.App.Services.Authentication;
using HexMaster.ThePrey.Maui.App.Services.Localization;
using HexMaster.ThePrey.Maui.App.Services.Navigation;
using HexMaster.ThePrey.Maui.App.Services.Platform;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HexMaster.ThePrey.Maui.App.ViewModels;

/// <summary>
/// Drives the game lobby. Resolves the current game (active → full read), shows the pass code + share
/// invite, the five owner-editable / non-owner-read-only settings selectors (ping seconds↔minutes at the
/// boundary), the participants list (name / role / ready), and the owner-only designate-hunter and START
/// actions plus the non-owner SET READY action. It never re-computes the backend's rules — it reflects
/// each <see cref="GameDetails"/> snapshot (load, command response, or live stream). Plain .NET (HTTP,
/// streaming, sharing, navigation, and options all behind interfaces) so it is fully unit-testable.
/// </summary>
public sealed class GameLobbyViewModel : ObservableObject
{
    public static readonly IReadOnlyList<int> DurationOptions = [30, 60, 90];
    public static readonly IReadOnlyList<int> HeadstartOptions = [5, 10, 15];
    public static readonly IReadOnlyList<int> EndgameOptions = [5, 10, 15];
    public static readonly IReadOnlyList<int> PingOptions = [2, 3, 5];
    public static readonly IReadOnlyList<int> EndgamePingOptions = [1, 2, 3, 5];

    /// <summary>Shell query-string key carrying the id of the game to open (set when navigating from create/join).</summary>
    public const string GameIdQueryKey = "gameId";

    private readonly IGameApiClient _gameApi;
    private readonly ILobbyStreamClient _stream;
    private readonly IShareService _shareService;
    private readonly ILobbyNavigator _navigator;
    private readonly IAccessTokenProvider _accessTokenProvider;
    private readonly ILocalizationService _localization;
    private readonly ThePreyClientOptions _options;
    private readonly ILogger<GameLobbyViewModel> _logger;

    private Guid? _targetGameId;
    private Guid? _gameId;
    private Guid? _hunterUserId;
    private string? _lastToken;
    private GameDetails? _lastSnapshot;
    private bool _seeding;
    private bool _handedOff;
    private bool _isReadyToStart;

    private CancellationTokenSource? _streamCts;

    private bool _isBusy;
    private bool _isLoaded;
    private bool _hasError;
    private bool _actionError;
    private string _passCode = string.Empty;
    private bool _isOwner;
    private int _selectedDuration = 30;
    private int _selectedHeadstart = 5;
    private int _selectedEndgame = 10;
    private int _selectedPing = 2;
    private int _selectedEndgamePing = 1;

    public GameLobbyViewModel(
        IGameApiClient gameApi,
        ILobbyStreamClient stream,
        IShareService shareService,
        ILobbyNavigator navigator,
        IAccessTokenProvider accessTokenProvider,
        ILocalizationService localization,
        IOptions<ThePreyClientOptions> options,
        ILogger<GameLobbyViewModel> logger)
    {
        _gameApi = gameApi;
        _stream = stream;
        _shareService = shareService;
        _navigator = navigator;
        _accessTokenProvider = accessTokenProvider;
        _localization = localization;
        _options = options.Value;
        _logger = logger;

        ShareCommand = new RelayCommand(ShareAsync, () => IsLoaded);
        SetReadyCommand = new RelayCommand(SetReadyAsync, () => ShowReady && !IsBusy);
        StartCommand = new RelayCommand(StartAsync, () => CanStart);
        DesignateHunterCommand = new RelayCommand<LobbyParticipant>(DesignateHunterAsync);
    }

    /// <summary>Participants shown in the list — name, role, ready state.</summary>
    public ObservableCollection<LobbyParticipant> Participants { get; } = [];

    /// <summary>Shares the invite (pass code + join link) through the native share sheet.</summary>
    public RelayCommand ShareCommand { get; }

    /// <summary>Non-owner readies up. Inert / hidden for the owner.</summary>
    public RelayCommand SetReadyCommand { get; }

    /// <summary>Owner starts the operation. Enabled only when the game reports it is ready to start.</summary>
    public RelayCommand StartCommand { get; }

    /// <summary>Owner taps a participant to designate the hunter. Inert for non-owners.</summary>
    public RelayCommand<LobbyParticipant> DesignateHunterCommand { get; }

    public IReadOnlyList<int> DurationChoices => DurationOptions;
    public IReadOnlyList<int> HeadstartChoices => HeadstartOptions;
    public IReadOnlyList<int> EndgameChoices => EndgameOptions;
    public IReadOnlyList<int> PingChoices => PingOptions;
    public IReadOnlyList<int> EndgamePingChoices => EndgamePingOptions;

    /// <summary>True while a load or a lobby action is in flight.</summary>
    public bool IsBusy
    {
        get => _isBusy;
        private set { if (SetProperty(ref _isBusy, value)) UpdateDerived(); }
    }

    /// <summary>True once a game snapshot has been loaded and is being shown.</summary>
    public bool IsLoaded
    {
        get => _isLoaded;
        private set { if (SetProperty(ref _isLoaded, value)) UpdateDerived(); }
    }

    /// <summary>True when the game could not be resolved/loaded (no active game, not found, unauthorized, transient).</summary>
    public bool HasError
    {
        get => _hasError;
        private set { if (SetProperty(ref _hasError, value)) UpdateDerived(); }
    }

    /// <summary>True when the last lobby action (settings/ready/designate/start) failed; a non-blocking hint.</summary>
    public bool ActionError
    {
        get => _actionError;
        private set => SetProperty(ref _actionError, value);
    }

    /// <summary>The game's secret pass code, shown verbatim.</summary>
    public string PassCode
    {
        get => _passCode;
        private set => SetProperty(ref _passCode, value);
    }

    /// <summary>True when the current user owns this game (drives editability and the owner-only actions).</summary>
    public bool IsOwner
    {
        get => _isOwner;
        private set { if (SetProperty(ref _isOwner, value)) UpdateDerived(); }
    }

    public int SelectedDuration
    {
        get => _selectedDuration;
        set { if (SetProperty(ref _selectedDuration, value)) OnSettingChanged(); }
    }

    public int SelectedHeadstart
    {
        get => _selectedHeadstart;
        set { if (SetProperty(ref _selectedHeadstart, value)) OnSettingChanged(); }
    }

    public int SelectedEndgame
    {
        get => _selectedEndgame;
        set { if (SetProperty(ref _selectedEndgame, value)) OnSettingChanged(); }
    }

    public int SelectedPing
    {
        get => _selectedPing;
        set { if (SetProperty(ref _selectedPing, value)) OnSettingChanged(); }
    }

    public int SelectedEndgamePing
    {
        get => _selectedEndgamePing;
        set { if (SetProperty(ref _selectedEndgamePing, value)) OnSettingChanged(); }
    }

    /// <summary>Content (header/settings/participants) is shown once loaded without a load error.</summary>
    public bool ShowContent => IsLoaded && !HasError;

    /// <summary>The five selectors are editable only for the owner.</summary>
    public bool SettingsEditable => IsOwner;

    /// <summary>The START OPERATION action is shown only to the owner.</summary>
    public bool ShowStart => IsLoaded && IsOwner;

    /// <summary>The SET READY action is shown only to non-owners.</summary>
    public bool ShowReady => IsLoaded && !IsOwner;

    /// <summary>Start is enabled only when the loaded game reports it is ready to start (and not busy).</summary>
    public bool CanStart => IsOwner && _isReadyToStart && !IsBusy;

    /// <summary>Exposed for tests: awaits the in-flight lobby-stream consumption started by <see cref="ActivateAsync"/>.</summary>
    internal Task? StreamTask { get; private set; }

    /// <summary>
    /// Sets the specific game the lobby should open, supplied by the page from the <c>gameId</c> navigation
    /// query when arriving from create/join. When set, <see cref="LoadAsync"/> loads this game directly by
    /// id rather than resolving the caller's active game — a just-created game is still in its lobby phase
    /// and <c>GET /games/active</c> (which reports only started games) would not return it.
    /// </summary>
    public void SetTargetGame(Guid? gameId) => _targetGameId = gameId;

    /// <summary>
    /// Called when the page appears: loads the current game, then (if it loaded and has not already handed
    /// off to gameplay) subscribes to the live lobby stream.
    /// </summary>
    public async Task ActivateAsync()
    {
        _handedOff = false;
        await LoadAsync();

        if (!HasError && !_handedOff && _gameId is Guid id && _lastToken is string token)
            StartStream(id, token);
    }

    /// <summary>Called when the page disappears: cancels the live lobby subscription.</summary>
    public void Deactivate() => StopStream();

    /// <summary>Resolves the active game and loads its full state, mapping each outcome to a distinct display state.</summary>
    public async Task LoadAsync()
    {
        IsBusy = true;
        HasError = false;
        ActionError = false;
        try
        {
            var token = await _accessTokenProvider.GetAccessTokenAsync();
            if (token is null)
            {
                SetLoadError();
                return;
            }

            _lastToken = token;

            var gameId = await ResolveGameIdAsync(token);
            if (gameId is not Guid id)
            {
                SetLoadError();
                return;
            }

            var game = await _gameApi.GetGameAsync(id, token);
            switch (game.Outcome)
            {
                case GetGameOutcome.Success when game.Game is not null:
                    // A load may resume a game that already left the lobby (started while we were away).
                    ApplySnapshot(game.Game, allowHandOff: true);
                    break;
                case GetGameOutcome.Unauthorized:
                    _accessTokenProvider.Invalidate();
                    SetLoadError();
                    break;
                default: // NotFound / Error / (null game)
                    SetLoadError();
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load the game lobby.");
            SetLoadError();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void SetLoadError()
    {
        IsLoaded = false;
        HasError = true;
    }

    // Resolves which game to load. A game we were navigated to explicitly (just created or joined) is
    // loaded by its id directly — it is still in its lobby phase and GET /games/active only reports
    // started games, so it would 404 for a fresh lobby. With no explicit target (resume from the menu),
    // fall back to the caller's active (started) game. Returns null (→ load error) when none resolves.
    private async Task<Guid?> ResolveGameIdAsync(string token)
    {
        if (_targetGameId is Guid target)
            return target;

        var active = await _gameApi.GetActiveGameAsync(token);
        switch (active.Outcome)
        {
            case ActiveGameOutcome.HasActiveGame when active.Game is not null:
                return active.Game.GameId;
            case ActiveGameOutcome.Unauthorized:
                _accessTokenProvider.Invalidate();
                return null;
            default: // NoActiveGame / Error / (null game)
                return null;
        }
    }

    // Replaces the whole VM state from a snapshot (load, command response, or stream event). Only a load
    // (resuming an already-started game) or a live stream frame (the game-started broadcast) may hand off
    // to gameplay — signalled by <paramref name="allowHandOff"/>. A lobby command response (designate
    // hunter, set ready, save settings) is always a pure lobby action that cannot start the game, so it
    // must never navigate, even if the server echoes back an unexpected status.
    private void ApplySnapshot(GameDetails game, bool allowHandOff = false)
    {
        _lastSnapshot = game;
        _gameId = game.Id;
        _hunterUserId = game.HunterUserId;
        _isReadyToStart = game.IsReadyToStart;

        PassCode = game.GameCode;
        IsOwner = game.IsOwnerPlayer;

        SeedSelectors(game.Configuration);
        RebuildParticipants(game);

        HasError = false;
        IsLoaded = true;
        UpdateDerived();

        // Only Started/InProgress hands off to gameplay. The automatic Lobby→Ready readiness transition
        // must never navigate anyone — it merely enables the owner's start button.
        if (allowHandOff && game.IsArmed)
            _ = HandOffAsync();
    }

    // Seeds the five selectors from the config, snapping to the nearest allowed option and suppressing the
    // change-triggered save while seeding. Ping intervals arrive in seconds and are shown in minutes.
    private void SeedSelectors(GameConfigurationDetails? config)
    {
        // A lobby-stream snapshot can be a partial frame that omits the configuration object (e.g. a
        // state-changed event that starts the game). Keep the last-seeded selector values rather than
        // dereferencing a null config — ApplySnapshot must still run its status/handoff logic below.
        if (config is null)
            return;

        _seeding = true;
        try
        {
            SelectedDuration = Snap(config.GameDuration, DurationOptions);
            SelectedHeadstart = Snap(config.HunterDelayTime, HeadstartOptions);
            SelectedEndgame = Snap(config.FinalStageDuration, EndgameOptions);
            SelectedPing = Snap(config.DefaultLocationInterval / 60, PingOptions);
            SelectedEndgamePing = Snap(config.FinalLocationInterval / 60, EndgamePingOptions);
        }
        finally
        {
            _seeding = false;
        }
    }

    private void RebuildParticipants(GameDetails game)
    {
        Participants.Clear();
        // Same partial-frame guard as SeedSelectors: a stream snapshot may omit the participant list.
        foreach (var participant in game.Participants ?? [])
            Participants.Add(new LobbyParticipant(participant, game.HunterUserId, game.OwnerUserId));
    }

    // Owner selector edits persist immediately; seeding and non-owner changes never trigger a save.
    private void OnSettingChanged()
    {
        if (_seeding || !IsOwner || !IsLoaded)
            return;

        _ = SaveSettingsAsync();
    }

    internal async Task SaveSettingsAsync()
    {
        if (_gameId is not Guid id)
            return;

        ActionError = false;
        var token = await _accessTokenProvider.GetAccessTokenAsync();
        if (token is null)
        {
            ActionError = true;
            RevertSelectors();
            return;
        }

        IsBusy = true;
        try
        {
            var settings = new GameSettingsParameters(
                SelectedDuration, SelectedHeadstart, SelectedEndgame, SelectedPing, SelectedEndgamePing);
            var result = await _gameApi.UpdateGameSettingsAsync(id, settings, token);
            switch (result.Outcome)
            {
                case UpdateGameSettingsOutcome.Success when result.Game is not null:
                    ApplySnapshot(result.Game);
                    break;
                case UpdateGameSettingsOutcome.Unauthorized:
                    _accessTokenProvider.Invalidate();
                    ActionError = true;
                    RevertSelectors();
                    break;
                default: // Validation / Forbidden / Error
                    ActionError = true;
                    RevertSelectors();
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save the game settings.");
            ActionError = true;
            RevertSelectors();
        }
        finally
        {
            IsBusy = false;
        }
    }

    // Re-seeds the selectors from the last server-known snapshot, discarding an unsaved local change.
    private void RevertSelectors()
    {
        if (_lastSnapshot is not null)
            SeedSelectors(_lastSnapshot.Configuration);
    }

    internal async Task DesignateHunterAsync(LobbyParticipant participant)
    {
        if (participant is null || !IsOwner || _gameId is not Guid id)
            return;

        ActionError = false;
        var token = await _accessTokenProvider.GetAccessTokenAsync();
        if (token is null)
        {
            ActionError = true;
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _gameApi.DesignateHunterAsync(id, participant.UserId, token);
            switch (result.Outcome)
            {
                case DesignateHunterOutcome.Success when result.Game is not null:
                    ApplySnapshot(result.Game);
                    break;
                case DesignateHunterOutcome.Unauthorized:
                    _accessTokenProvider.Invalidate();
                    ActionError = true;
                    break;
                default: // Forbidden / NotFound / Error
                    ActionError = true;
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to designate the hunter.");
            ActionError = true;
        }
        finally
        {
            IsBusy = false;
        }
    }

    internal async Task SetReadyAsync()
    {
        if (IsOwner || _gameId is not Guid id)
            return;

        ActionError = false;
        var token = await _accessTokenProvider.GetAccessTokenAsync();
        if (token is null)
        {
            ActionError = true;
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _gameApi.SetReadyAsync(id, token);
            switch (result.Outcome)
            {
                case SetReadyOutcome.Success when result.Game is not null:
                    ApplySnapshot(result.Game);
                    break;
                case SetReadyOutcome.Unauthorized:
                    _accessTokenProvider.Invalidate();
                    ActionError = true;
                    break;
                default: // Forbidden / NotFound / Error
                    ActionError = true;
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to set ready.");
            ActionError = true;
        }
        finally
        {
            IsBusy = false;
        }
    }

    internal async Task StartAsync()
    {
        if (!CanStart || _gameId is not Guid id || _hunterUserId is not Guid hunter)
            return;

        ActionError = false;
        var token = await _accessTokenProvider.GetAccessTokenAsync();
        if (token is null)
        {
            ActionError = true;
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _gameApi.StartGameAsync(id, hunter, token);
            switch (result.Outcome)
            {
                case StartGameOutcome.Success:
                    await HandOffAsync();
                    break;
                case StartGameOutcome.Unauthorized:
                    _accessTokenProvider.Invalidate();
                    ActionError = true;
                    break;
                default: // Validation / Forbidden / NotFound / Error — the next snapshot re-syncs enablement.
                    ActionError = true;
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to start the operation.");
            ActionError = true;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ShareAsync()
    {
        if (string.IsNullOrEmpty(PassCode) || _gameId is not Guid gameId)
            return;

        // The invite link carries the game id (a Guid) — the deep-link handler only accepts
        // https://theprey.nl/join/{gameId}. The pass code is shared as text for the recipient to type in.
        var link = $"{_options.JoinLinkBaseUrl.TrimEnd('/')}/{gameId}";
        var text = string.Format(_localization["Lobby_Invite_Template"], PassCode, link);
        var title = _localization["Lobby_Invite_Title"];
        await _shareService.ShareTextAsync(title, text);
    }

    private async Task HandOffAsync()
    {
        if (_handedOff)
            return;

        _handedOff = true;
        await _navigator.GoToGameplayAsync();
    }

    private void StartStream(Guid gameId, string accessToken)
    {
        StopStream();
        var cts = new CancellationTokenSource();
        _streamCts = cts;
        StreamTask = ConsumeStreamAsync(gameId, accessToken, cts.Token);
    }

    private async Task ConsumeStreamAsync(Guid gameId, string accessToken, CancellationToken ct)
    {
        try
        {
            await foreach (var snapshot in _stream.Subscribe(gameId, accessToken, ct).WithCancellation(ct))
            {
                if (_handedOff)
                    break;
                // The live stream carries the game-started broadcast — the genuine signal to hand off.
                ApplySnapshot(snapshot, allowHandOff: true);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal on deactivate.
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "The lobby stream ended unexpectedly.");
        }
    }

    private void StopStream()
    {
        _streamCts?.Cancel();
        _streamCts?.Dispose();
        _streamCts = null;
    }

    private void UpdateDerived()
    {
        OnPropertyChanged(nameof(ShowContent));
        OnPropertyChanged(nameof(SettingsEditable));
        OnPropertyChanged(nameof(ShowStart));
        OnPropertyChanged(nameof(ShowReady));
        OnPropertyChanged(nameof(CanStart));

        ShareCommand.RaiseCanExecuteChanged();
        SetReadyCommand.RaiseCanExecuteChanged();
        StartCommand.RaiseCanExecuteChanged();
    }

    // Returns the allowed option closest to a stored value, so a value written by another client that is
    // not in the fixed set still displays on a selector rather than leaving it blank.
    private static int Snap(int value, IReadOnlyList<int> options)
    {
        var best = options[0];
        var bestDistance = Math.Abs(value - best);
        for (var i = 1; i < options.Count; i++)
        {
            var distance = Math.Abs(value - options[i]);
            if (distance < bestDistance)
            {
                best = options[i];
                bestDistance = distance;
            }
        }

        return best;
    }
}
