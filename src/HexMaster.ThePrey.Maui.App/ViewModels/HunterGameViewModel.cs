using HexMaster.ThePrey.Maui.App.Services.Api;
using HexMaster.ThePrey.Maui.App.Services.Authentication;
using HexMaster.ThePrey.Maui.App.Services.Localization;
using HexMaster.ThePrey.Maui.App.Services.Location;
using HexMaster.ThePrey.Maui.App.Services.Navigation;
using Microsoft.Extensions.Logging;

namespace HexMaster.ThePrey.Maui.App.ViewModels;

/// <summary>
/// Drives the full-screen hunter game play page. Resolves the caller's active game, derives the
/// <see cref="GamePhase"/> machine (Waiting → HeadStart → Live → Ended) from the game's status and
/// <c>HunterMayMoveAt</c>, seeds the map from the rich status snapshot (playfield polygon + prey dots),
/// keeps it live from the Web PubSub channel + a periodic status re-poll, renders the self position +
/// compass heading, runs the head-start countdown via <see cref="TimeProvider"/>, and hands off once on
/// game-ended. All HTTP, streaming, position, heading, navigation, and time are behind interfaces /
/// <see cref="TimeProvider"/> so the whole view model is unit-testable.
/// </summary>
public sealed class HunterGameViewModel : ObservableObject, IDisposable
{
    private static readonly TimeSpan OneSecond = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan RepollInterval = TimeSpan.FromSeconds(15);

    private readonly IGameApiClient _gameApi;
    private readonly IGameStreamClient _stream;
    private readonly ILivePositionReader _positionReader;
    private readonly IHeadingReader _headingReader;
    private readonly IGameplayNavigator _navigator;
    private readonly IAccessTokenProvider _accessTokenProvider;
    private readonly ILocalizationService _localization;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<HunterGameViewModel> _logger;

    private readonly Dictionary<Guid, MapBlip> _blipsById = new();

    private CancellationTokenSource? _lifecycleCts;
    private Task? _streamTask;
    private ITimer? _tickTimer;
    private ITimer? _repollTimer;

    private Guid _gameId;
    private Guid? _hunterUserId;
    private string _status = "Ready";
    private DateTimeOffset? _hunterMayMoveAt;
    private bool _handedOff;
    private bool _readersStarted;

    private GamePhase _phase = GamePhase.Waiting;
    private string _headStartCountdownText = "00:00";
    private bool _isBusy;
    private string? _errorMessage;

    public HunterGameViewModel(
        IGameApiClient gameApi,
        IGameStreamClient stream,
        ILivePositionReader positionReader,
        IHeadingReader headingReader,
        IGameplayNavigator navigator,
        IAccessTokenProvider accessTokenProvider,
        ILocalizationService localization,
        TimeProvider timeProvider,
        ILogger<HunterGameViewModel> logger)
    {
        _gameApi = gameApi;
        _stream = stream;
        _positionReader = positionReader;
        _headingReader = headingReader;
        _navigator = navigator;
        _accessTokenProvider = accessTokenProvider;
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

    /// <summary>The hunter head-start overlay always shows the move-early / 10-minute-penalty warning.</summary>
    public bool ShowPenaltyWarning => _phase == GamePhase.HeadStart;

    public bool IsLive => _phase == GamePhase.Live;

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

    /// <summary>The prey dots to draw (red active / grey caught); the hunter's own row is never here.</summary>
    public IReadOnlyList<MapBlip> Blips => _blipsById.Values.ToArray();

    /// <summary>The hunter's own current local position (the green self arrow), or <c>null</c> before the first fix.</summary>
    public GpsFix? SelfPosition { get; private set; }

    /// <summary>The device compass heading in degrees, or <c>null</c> when unavailable.</summary>
    public double? Heading { get; private set; }

    /// <summary>Resolves the active game and its role/phase, then seeds the map from the status snapshot.</summary>
    public async Task LoadAsync(CancellationToken ct = default)
    {
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var token = await _accessTokenProvider.GetAccessTokenAsync(ct);
            if (token is null)
            {
                SetError("HunterGame_Error_Unauthorized");
                return;
            }

            var active = await _gameApi.GetActiveGameAsync(token, ct);
            switch (active.Outcome)
            {
                case ActiveGameOutcome.HasActiveGame when active.Game is not null:
                    _gameId = active.Game.GameId;
                    break;
                case ActiveGameOutcome.NoActiveGame:
                    SetError("HunterGame_Error_NoGame");
                    return;
                case ActiveGameOutcome.Unauthorized:
                    _accessTokenProvider.Invalidate();
                    SetError("HunterGame_Error_Unauthorized");
                    return;
                default:
                    SetError("HunterGame_Error_Generic");
                    return;
            }

            var game = await _gameApi.GetGameAsync(_gameId, token, ct);
            switch (game.Outcome)
            {
                case GetGameOutcome.Success when game.Game is not null:
                    ApplyGame(game.Game);
                    break;
                case GetGameOutcome.NotFound:
                    SetError("HunterGame_Error_NotFound");
                    return;
                case GetGameOutcome.Unauthorized:
                    _accessTokenProvider.Invalidate();
                    SetError("HunterGame_Error_Unauthorized");
                    return;
                default:
                    SetError("HunterGame_Error_Generic");
                    return;
            }

            if (IsInProgressPhase)
                await RefreshStatusAsync(token, ct);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Loads the game, then starts the live channel, position/heading readers, and the timers.</summary>
    public async Task ActivateAsync()
    {
        _lifecycleCts?.Cancel();
        _lifecycleCts?.Dispose();
        _lifecycleCts = new CancellationTokenSource();

        await LoadAsync(_lifecycleCts.Token);

        if (_handedOff || HasError)
            return;

        StartReaders();

        var token = await _accessTokenProvider.GetAccessTokenAsync(_lifecycleCts.Token);
        if (token is not null)
            _streamTask = Task.Run(() => ConsumeStreamAsync(_gameId, token, _lifecycleCts.Token), CancellationToken.None);

        _tickTimer ??= _timeProvider.CreateTimer(_ => Tick(), null, OneSecond, OneSecond);
        _repollTimer ??= _timeProvider.CreateTimer(_ => _ = SafeRefreshAsync(), null, RepollInterval, RepollInterval);
    }

    /// <summary>Stops the channel, readers, and timers; safe to call more than once.</summary>
    public void Deactivate()
    {
        _lifecycleCts?.Cancel();
        StopReaders();
        _tickTimer?.Dispose();
        _tickTimer = null;
        _repollTimer?.Dispose();
        _repollTimer = null;
    }

    private bool IsInProgressPhase => _phase is GamePhase.HeadStart or GamePhase.Live;

    private void ApplyGame(GameDetails game)
    {
        _hunterUserId = game.HunterUserId;
        _status = game.Status;
        RecomputePhase();
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
            return;
        }

        // Lobby / Ready — armed, not yet committed by the sweep.
        Phase = GamePhase.Waiting;
    }

    private async Task RefreshStatusAsync(string token, CancellationToken ct)
    {
        var result = await _gameApi.GetGameStatusDetailsAsync(_gameId, token, ct);
        switch (result.Outcome)
        {
            case GetGameStatusOutcome.Success when result.Details is not null:
                SeedFromStatus(result.Details);
                break;

            case GetGameStatusOutcome.Forbidden:
            case GetGameStatusOutcome.Conflict:
                // Not live yet (game still transitioning) — keep Waiting/HeadStart and re-poll next cadence.
                _logger.LogInformation("Hunter status not available yet ({Outcome}); staying in {Phase}.", result.Outcome, _phase);
                break;

            case GetGameStatusOutcome.Unauthorized:
                _accessTokenProvider.Invalidate();
                SetError("HunterGame_Error_Unauthorized");
                break;

            case GetGameStatusOutcome.NotFound:
                SetError("HunterGame_Error_NotFound");
                break;

            default:
                _logger.LogWarning("Hunter status refresh returned {Outcome}; keeping last values.", result.Outcome);
                break;
        }
    }

    private void SeedFromStatus(GameStatusDetails status)
    {
        _hunterUserId ??= status.HunterUserId;
        _hunterMayMoveAt = status.HunterMayMoveAt;
        PlayfieldPolygon = status.PlayfieldCoordinates;

        _blipsById.Clear();
        foreach (var participant in status.Participants)
        {
            var blip = GameMapProjection.ProjectForHunter(
                participant.UserId, _hunterUserId, participant.LastKnownLocation, participant.State);
            if (blip is not null)
                _blipsById[blip.Id] = blip;
        }

        RecomputePhase();
        RaiseMapChanged();
    }

    // --- Live channel ---

    private async Task ConsumeStreamAsync(Guid gameId, string token, CancellationToken ct)
    {
        try
        {
            await foreach (var evt in _stream.Subscribe(gameId, token, ct))
                ApplyEvent(evt);
        }
        catch (OperationCanceledException)
        {
            // Deactivated — expected.
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Hunter game stream ended unexpectedly.");
        }
    }

    private void ApplyEvent(GameStreamEvent evt)
    {
        switch (evt)
        {
            case GameStreamEvent.ParticipantLocated located:
                ApplyLocated(located);
                break;

            case GameStreamEvent.ParticipantStatusChanged status:
                ApplyStatusChanged(status);
                break;

            case GameStreamEvent.StateChanged state:
                ApplyStateChanged(state);
                break;

            case GameStreamEvent.GameEnded:
                Phase = GamePhase.Ended;
                HandOffOnce();
                break;
        }
    }

    private void ApplyLocated(GameStreamEvent.ParticipantLocated located)
    {
        var blip = GameMapProjection.ProjectForHunter(
            located.UserId, _hunterUserId, new GpsCoordinate(located.Latitude, located.Longitude), located.State ?? "Active");
        if (blip is null)
            return; // The hunter's own location is the self arrow, not a dot.
        _blipsById[blip.Id] = blip;
        RaiseMapChanged();
    }

    private void ApplyStatusChanged(GameStreamEvent.ParticipantStatusChanged status)
    {
        if (!_blipsById.TryGetValue(status.ParticipantId, out var existing))
            return; // No dot to recolor yet — a later located/re-poll will place it.
        var role = GameMapProjection.IsCaught(status.NewState) ? MapBlipRole.Caught : MapBlipRole.Prey;
        _blipsById[status.ParticipantId] = existing with { Role = role };
        RaiseMapChanged();
    }

    private void ApplyStateChanged(GameStreamEvent.StateChanged state)
    {
        _status = state.NewState;
        RecomputePhase();
        if (IsInProgressPhase)
            _ = SafeRefreshAsync(); // Ready→InProgress edge: re-poll to seed the snapshot.
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

    // --- Countdown + re-poll timers ---

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

    private async Task SafeRefreshAsync()
    {
        var ct = _lifecycleCts?.Token ?? CancellationToken.None;
        try
        {
            var token = await _accessTokenProvider.GetAccessTokenAsync(ct);
            if (token is not null)
                await RefreshStatusAsync(token, ct);
        }
        catch (OperationCanceledException)
        {
            // Deactivated mid-refresh.
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Scheduled hunter status refresh failed.");
        }
    }

    private void HandOffOnce()
    {
        if (_handedOff)
            return;
        _handedOff = true;
        _ = _navigator.GoToOutcomeAsync();
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
