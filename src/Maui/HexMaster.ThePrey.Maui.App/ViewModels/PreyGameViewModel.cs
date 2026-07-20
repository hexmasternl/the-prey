using HexMaster.ThePrey.Maui.App.Services.Api;
using HexMaster.ThePrey.Maui.App.Services.Localization;
using HexMaster.ThePrey.Maui.App.Services.Location;
using HexMaster.ThePrey.Maui.App.Services.Navigation;
using HexMaster.ThePrey.Maui.App.Services.Realtime;
using HexMaster.ThePrey.Maui.App.Services.Session;
using Microsoft.Extensions.Logging;

namespace HexMaster.ThePrey.Maui.App.ViewModels;

/// <summary>
/// Drives the full-screen prey game play page. Mirrors <see cref="HunterGameViewModel"/> — the same
/// Waiting → HeadStart → Live → Ended phase machine, self position/heading, and head-start countdown — but
/// with the prey map projection (the hunter is a red dot, other preys are green, caught preys grey, self is
/// the green arrow) and a prey-only <see cref="Spectating"/> state: when this player is tagged/out while the
/// game runs, it keeps every connection alive and hands off only on game-ended. All game data flows from the
/// shared <see cref="IGameStateService"/> store; position, heading, navigation, identity, and time sit behind
/// interfaces / <see cref="TimeProvider"/> so the whole view model is unit-testable.
/// </summary>
public sealed class PreyGameViewModel : ObservableObject, IDisposable
{
    private static readonly TimeSpan OneSecond = TimeSpan.FromSeconds(1);

    private readonly IGameStateService _stateService;
    private readonly ILivePositionReader _positionReader;
    private readonly IHeadingReader _headingReader;
    private readonly IGameplayNavigator _navigator;
    private readonly ICurrentUserProvider _currentUser;
    private readonly IGameLocationTracker _locationTracker;
    private readonly ILocalizationService _localization;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<PreyGameViewModel> _logger;

    private bool _trackingStarted;
    private bool _subscribed;

    private CancellationTokenSource? _lifecycleCts;
    private ITimer? _tickTimer;

    private Guid _gameId;
    private Guid? _hunterUserId;
    private Guid _selfUserId;
    private string _status = "Ready";
    private DateTimeOffset? _hunterMayMoveAt;
    private int _gameDurationLeftSeconds;
    private bool _handedOff;
    private bool _readersStarted;

    private GamePhase _phase = GamePhase.Waiting;
    private string _headStartCountdownText = "00:00";
    private bool _isBusy;
    private bool _spectating;
    private string? _errorMessage;

    public PreyGameViewModel(
        IGameStateService stateService,
        ILivePositionReader positionReader,
        IHeadingReader headingReader,
        IGameplayNavigator navigator,
        ICurrentUserProvider currentUser,
        IGameLocationTracker locationTracker,
        ILocalizationService localization,
        TimeProvider timeProvider,
        ILogger<PreyGameViewModel> logger)
    {
        _stateService = stateService;
        _positionReader = positionReader;
        _headingReader = headingReader;
        _navigator = navigator;
        _currentUser = currentUser;
        _locationTracker = locationTracker;
        _localization = localization;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <summary>Raised when the polygon, blips, or self position/heading change, so the page redraws the map.</summary>
    public event EventHandler? MapChanged;

    public Guid GameId => _gameId;

    public GamePhase Phase
    {
        get => _phase;
        private set
        {
            if (SetProperty(ref _phase, value))
            {
                OnPropertyChanged(nameof(ShowWaitingOverlay));
                OnPropertyChanged(nameof(ShowHeadStartOverlay));
                OnPropertyChanged(nameof(ShowPenaltyWarning));
                OnPropertyChanged(nameof(IsLive));
            }
        }
    }

    public bool ShowWaitingOverlay => _phase == GamePhase.Waiting;
    public bool ShowHeadStartOverlay => _phase == GamePhase.HeadStart;

    /// <summary>The prey head-start overlay shows the (prey-framed) hunter-must-not-move / penalty warning.</summary>
    public bool ShowPenaltyWarning => _phase == GamePhase.HeadStart;

    public bool IsLive => _phase == GamePhase.Live;

    /// <summary>True once this player has been tagged/out while the game runs — a spectator until game-ended.</summary>
    public bool Spectating
    {
        get => _spectating;
        private set => SetProperty(ref _spectating, value);
    }

    public string HeadStartCountdownText
    {
        get => _headStartCountdownText;
        private set => SetProperty(ref _headStartCountdownText, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (SetProperty(ref _errorMessage, value))
                OnPropertyChanged(nameof(HasError));
        }
    }

    public bool HasError => !string.IsNullOrEmpty(_errorMessage);

    /// <summary>The green playfield polygon vertices (drawn once); empty until the status snapshot loads.</summary>
    public IReadOnlyList<GpsCoordinate> PlayfieldPolygon { get; private set; } = Array.Empty<GpsCoordinate>();

    /// <summary>The player dots (red hunter / green other-prey / grey caught); the caller's own row is never here.</summary>
    public IReadOnlyList<MapBlip> Blips { get; private set; } = Array.Empty<MapBlip>();

    /// <summary>The prey's own current local position (the green self arrow), or <c>null</c> before the first fix.</summary>
    public GpsFix? SelfPosition { get; private set; }

    /// <summary>The device compass heading in degrees, or <c>null</c> when unavailable.</summary>
    public double? Heading { get; private set; }

    /// <summary>
    /// Resolves the caller identity, starts the shared game-state store (which resolves the active game and
    /// seeds the first snapshot), then projects that snapshot onto the map. Sets an error when there is no
    /// active game to resume.
    /// </summary>
    public async Task LoadAsync(CancellationToken ct = default)
    {
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            _selfUserId = await _currentUser.GetUserIdAsync(ct) ?? _selfUserId;

            var state = await _stateService.StartAsync(ct);
            if (state is null)
            {
                SetError("PreyGame_Error_NoGame");
                return;
            }

            ApplyState(state);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Loads the game, subscribes to live state, and starts the position/heading readers and timers.</summary>
    public async Task ActivateAsync()
    {
        _lifecycleCts?.Cancel();
        _lifecycleCts?.Dispose();
        _lifecycleCts = new CancellationTokenSource();

        await LoadAsync(_lifecycleCts.Token);

        if (_handedOff || HasError)
            return;

        Subscribe();
        // Apply the freshest snapshot in case it advanced between the seed and the subscription.
        if (_stateService.CurrentState is { } latest)
            ApplyState(latest);

        StartReaders();

        _tickTimer ??= _timeProvider.CreateTimer(_ => Tick(), null, OneSecond, OneSecond);
    }

    /// <summary>Unsubscribes, stops the store connection, the readers, and the timer; safe to call more than once.</summary>
    public void Deactivate()
    {
        _lifecycleCts?.Cancel();
        Unsubscribe();
        _ = _stateService.StopAsync();
        StopReaders();
        _tickTimer?.Dispose();
        _tickTimer = null;
    }

    private void Subscribe()
    {
        if (_subscribed)
            return;
        _subscribed = true;
        _stateService.Subscribe(OnStateChanged);
    }

    private void Unsubscribe()
    {
        if (!_subscribed)
            return;
        _subscribed = false;
        _stateService.Unsubscribe(OnStateChanged);
    }

    private void OnStateChanged(GameStateChanged change) => ApplyState(change.State);

    // Projects one store snapshot onto the map: status/phase, the polygon, and the prey-perspective dots.
    private void ApplyState(GameLiveState state)
    {
        _gameId = state.GameId;
        _hunterUserId = state.HunterUserId;
        _status = state.Status;
        _hunterMayMoveAt = state.HunterMayMoveAt;
        _gameDurationLeftSeconds = state.GameDurationLeft;
        PlayfieldPolygon = state.PlayfieldCoordinates;

        var blips = new List<MapBlip>(state.Participants.Count);
        foreach (var participant in state.Participants)
        {
            // Detect our own tagged/out state → spectator (keep the session alive).
            if (participant.UserId == _selfUserId && GameMapProjection.IsCaught(participant.State))
                Spectating = true;

            var blip = GameMapProjection.ProjectForPrey(
                participant.UserId, _selfUserId, _hunterUserId, participant.Location, participant.State);
            if (blip is not null)
                blips.Add(blip);
        }

        Blips = blips;
        RecomputePhase();
        RaiseMapChanged();
    }

    private void RecomputePhase()
    {
        if (string.Equals(_status, "Completed", StringComparison.OrdinalIgnoreCase))
        {
            Phase = GamePhase.Ended;
            HandOffOnce();
            return;
        }

        if (string.Equals(_status, "InProgress", StringComparison.OrdinalIgnoreCase))
        {
            var now = _timeProvider.GetUtcNow();
            Phase = _hunterMayMoveAt is { } mayMove && mayMove > now ? GamePhase.HeadStart : GamePhase.Live;
            UpdateCountdown(now);
            EnsureLocationTracking();
            return;
        }

        // Started — armed by the owner, awaiting the sweep's commit to InProgress.
        Phase = GamePhase.Waiting;
    }

    // --- Position + heading ---

    private void StartReaders()
    {
        if (_readersStarted)
            return;
        _readersStarted = true;
        _positionReader.PositionChanged += OnPositionChanged;
        _headingReader.HeadingChanged += OnHeadingChanged;
        _positionReader.Start();
        _headingReader.Start();
    }

    private void StopReaders()
    {
        if (!_readersStarted)
            return;
        _readersStarted = false;
        _positionReader.PositionChanged -= OnPositionChanged;
        _headingReader.HeadingChanged -= OnHeadingChanged;
        _positionReader.Stop();
        _headingReader.Stop();
    }

    private void OnPositionChanged(GpsFix fix)
    {
        SelfPosition = fix;
        RaiseMapChanged();
    }

    private void OnHeadingChanged(double heading)
    {
        Heading = heading;
        RaiseMapChanged();
    }

    // --- Countdown timer ---

    internal void Tick()
    {
        if (_phase != GamePhase.HeadStart)
            return;
        UpdateCountdown(_timeProvider.GetUtcNow());
    }

    private void UpdateCountdown(DateTimeOffset now)
    {
        if (_hunterMayMoveAt is not { } mayMove)
        {
            HeadStartCountdownText = "00:00";
            return;
        }

        var remaining = mayMove - now;
        if (remaining <= TimeSpan.Zero)
        {
            HeadStartCountdownText = "00:00";
            if (_phase == GamePhase.HeadStart)
                Phase = GamePhase.Live;
            return;
        }

        HeadStartCountdownText = $"{(int)remaining.TotalMinutes:00}:{remaining.Seconds:00}";
    }

    // Starts background location reporting once the game is InProgress. Idempotent at the tracker level;
    // the local guard avoids spawning a fire-and-forget call on every phase recompute. Reporting is
    // deliberately independent of this page's lifetime — it survives backgrounding until the game ends.
    private void EnsureLocationTracking()
    {
        if (_trackingStarted || _gameId == Guid.Empty)
            return;
        _trackingStarted = true;
        // Hand the tracker the game's remaining duration so it stops itself at game end even if this page
        // is gone and the server's game-over signal never reaches the background loop.
        var remaining = _gameDurationLeftSeconds > 0 ? TimeSpan.FromSeconds(_gameDurationLeftSeconds) : (TimeSpan?)null;
        _ = _locationTracker.StartAsync(_gameId, remaining);
    }

    private void HandOffOnce()
    {
        if (_handedOff)
            return;
        _handedOff = true;
        // The game has ended — stop background reporting and release the wake-lock/notification.
        _ = _locationTracker.StopAsync();
        _ = _navigator.GoToOutcomeAsync(_gameId, isHunter: false);
    }

    private void RaiseMapChanged() => MapChanged?.Invoke(this, EventArgs.Empty);

    private void SetError(string key)
    {
        ErrorMessage = _localization[key];
    }

    public void Dispose()
    {
        Deactivate();
        _lifecycleCts?.Dispose();
        _lifecycleCts = null;
    }
}
