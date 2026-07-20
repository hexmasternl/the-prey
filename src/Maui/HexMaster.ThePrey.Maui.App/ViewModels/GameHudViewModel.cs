using System.Globalization;
using System.Windows.Input;
using HexMaster.ThePrey.Maui.App.Services.Api;
using HexMaster.ThePrey.Maui.App.Services.Authentication;
using HexMaster.ThePrey.Maui.App.Services.Dialogs;
using HexMaster.ThePrey.Maui.App.Services.Localization;
using HexMaster.ThePrey.Maui.App.Services.Location;
using HexMaster.ThePrey.Maui.App.Services.Navigation;
using HexMaster.ThePrey.Maui.App.Services.Realtime;
using HexMaster.ThePrey.Maui.App.Services.Session;
using Microsoft.Extensions.Logging;

namespace HexMaster.ThePrey.Maui.App.ViewModels;

/// <summary>
/// Drives the in-game HUD overlay hosted by the (separately-owned) gameplay map page. Projects the
/// game clock, next-ping countdown, preys-active count and the role-aware nearest-adversary distance from
/// the shared <see cref="IGameStateService"/> store — the same snapshot the map reads — and ticks the two
/// countdowns locally each second between store updates. Owns the collapse/expand and Center follow toggle,
/// reads the device fix for the distance metric, and orchestrates the hunter's Tag flow. All store access,
/// HTTP, GPS, dialogs, the map signal, and time sit behind interfaces / <see cref="TimeProvider"/> so the
/// whole VM is unit-testable.
/// </summary>
public sealed class GameHudViewModel : ObservableObject, IDisposable
{
    private static readonly TimeSpan OneSecond = TimeSpan.FromSeconds(1);

    /// <summary>Re-read the device GPS every this many ticks, so our own movement keeps the distance fresh.</summary>
    private const int GpsRefreshTicks = 3;

    /// <summary>Fixed bar length (seconds) while the local player is under a boundary penalty.</summary>
    private const int PenaltyBarSeconds = 30;

    private readonly IGameStateService _stateService;
    private readonly IGameApiClient _gameApi;
    private readonly IAccessTokenProvider _accessTokenProvider;
    private readonly ICurrentUserProvider _currentUser;
    private readonly IGpsReader _gpsReader;
    private readonly IMapCameraController _mapCamera;
    private readonly ITagDialog _tagDialog;
    private readonly IConfirmationDialog _confirmationDialog;
    private readonly ILocalizationService _localization;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<GameHudViewModel> _logger;

    private ITimer? _tickTimer;
    private CancellationTokenSource? _lifecycleCts;
    private bool _subscribed;
    private int _ticksSinceGps;

    // The local player, so we can tell whether the penalty regime applies to us.
    private Guid _selfUserId;

    // The game clock, ticked locally between updates.
    private int _gameDurationLeft;
    private bool _hasSnapshot;

    // Latest ping-cadence inputs from the server, retained so the countdown can be re-seeded on a genuine
    // change (a reconcile or a penalty-regime flip) without snapping backward on every intervening delta.
    private int _serverNextPing;
    private int _serverNextPingWithPenalty;
    private int _serverPingInterval;
    private int _serverGameDurationLeft;
    private DateTimeOffset? _penaltyEndsAt;
    private bool _isPenalised;
    private bool _pingSeeded;

    // The locally-ticked next-ping countdown and the bar length it loops around (the effective interval,
    // which is 30s under penalty and the server's reporting interval otherwise).
    private int _nextPingRemaining;
    private int _pingBarMax = PenaltyBarSeconds;

    private int _preysLeft;
    private int _totalPreys;

    // Distance inputs.
    private Guid? _hunterUserId;
    private IReadOnlyList<GameLiveParticipant> _participants = Array.Empty<GameLiveParticipant>();
    private int? _hunterDistanceMeters;   // server-computed prey→hunter distance (fallback)
    private GpsFix? _deviceFix;

    private bool _isExpanded;
    private bool _isFollowingLocation = true;
    private bool _hasEnded;

    private string _gameTimeRemainingText = string.Empty;
    private string _preysActiveText = string.Empty;
    private string _distanceText = string.Empty;
    private string _nextPingRemainingText = string.Empty;
    private double _nextPingProgress;
    private bool _isPenaltyActive;
    private string _penaltyRemainingText = string.Empty;
    private string? _statusMessage;
    private bool _statusIsError;

    public GameHudViewModel(
        IGameStateService stateService,
        IGameApiClient gameApi,
        IAccessTokenProvider accessTokenProvider,
        ICurrentUserProvider currentUser,
        IGpsReader gpsReader,
        IMapCameraController mapCamera,
        ITagDialog tagDialog,
        IConfirmationDialog confirmationDialog,
        ILocalizationService localization,
        TimeProvider timeProvider,
        ILogger<GameHudViewModel> logger)
    {
        _stateService = stateService;
        _gameApi = gameApi;
        _accessTokenProvider = accessTokenProvider;
        _currentUser = currentUser;
        _gpsReader = gpsReader;
        _mapCamera = mapCamera;
        _tagDialog = tagDialog;
        _confirmationDialog = confirmationDialog;
        _localization = localization;
        _timeProvider = timeProvider;
        _logger = logger;

        ExpandCommand = new RelayCommand(() => { IsExpanded = true; return Task.CompletedTask; });
        CollapseCommand = new RelayCommand(() => { IsExpanded = false; return Task.CompletedTask; });
        ToggleCenterCommand = new RelayCommand(() => { ToggleCenter(); return Task.CompletedTask; });
        TagCommand = new RelayCommand(TagAsync, () => IsHunter);

        _distanceText = _localization["Hud_Distance_Unknown"];
    }

    /// <summary>The game whose HUD this is. Set by <see cref="Initialize"/> before activation.</summary>
    public Guid GameId { get; private set; }

    /// <summary>True when the local player is the hunter — governs Tag visibility and the distance metric.</summary>
    public bool IsHunter { get; private set; }

    /// <summary>Raised once, when the store reports the game is completed, so the host can hand off.</summary>
    public event EventHandler? GameEnded;

    public ICommand ExpandCommand { get; }
    public ICommand CollapseCommand { get; }
    public ICommand ToggleCenterCommand { get; }
    public RelayCommand TagCommand { get; }

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (SetProperty(ref _isExpanded, value))
                OnPropertyChanged(nameof(IsCollapsed));
        }
    }

    /// <summary>The inverse of <see cref="IsExpanded"/>, for toggling the collapsed content's visibility.</summary>
    public bool IsCollapsed => !_isExpanded;

    public bool IsFollowingLocation
    {
        get => _isFollowingLocation;
        private set => SetProperty(ref _isFollowingLocation, value);
    }

    public bool HasEnded
    {
        get => _hasEnded;
        private set => SetProperty(ref _hasEnded, value);
    }

    public string GameTimeRemainingText
    {
        get => _gameTimeRemainingText;
        private set => SetProperty(ref _gameTimeRemainingText, value);
    }

    public string PreysActiveText
    {
        get => _preysActiveText;
        private set => SetProperty(ref _preysActiveText, value);
    }

    public string DistanceText
    {
        get => _distanceText;
        private set => SetProperty(ref _distanceText, value);
    }

    public string NextPingRemainingText
    {
        get => _nextPingRemainingText;
        private set => SetProperty(ref _nextPingRemainingText, value);
    }

    /// <summary>The next-ping progress fraction (0..1): remaining seconds over the full ping interval.</summary>
    public double NextPingProgress
    {
        get => _nextPingProgress;
        private set => SetProperty(ref _nextPingProgress, value);
    }

    /// <summary>
    /// True while the local player's own boundary penalty is in effect — drives the top penalty banner.
    /// Clock-driven, so it becomes false the moment the penalty expires without needing a server event.
    /// </summary>
    public bool IsPenalised
    {
        get => _isPenaltyActive;
        private set => SetProperty(ref _isPenaltyActive, value);
    }

    /// <summary>
    /// The mm:ss countdown of the local player's remaining penalty time; empty while not penalised.
    /// Recomputed from the absolute <c>PenaltyEndsAt</c> each tick, so it never drifts.
    /// </summary>
    public string PenaltyRemainingText
    {
        get => _penaltyRemainingText;
        private set => SetProperty(ref _penaltyRemainingText, value);
    }

    /// <summary>The most recent transient user message (tag feedback or an error), or <c>null</c>.</summary>
    public string? StatusMessage
    {
        get => _statusMessage;
        private set
        {
            if (SetProperty(ref _statusMessage, value))
                OnPropertyChanged(nameof(HasStatusMessage));
        }
    }

    public bool HasStatusMessage => !string.IsNullOrEmpty(_statusMessage);

    /// <summary>True when <see cref="StatusMessage"/> represents an error rather than informational feedback.</summary>
    public bool StatusIsError
    {
        get => _statusIsError;
        private set => SetProperty(ref _statusIsError, value);
    }

    /// <summary>Supplies the runtime identity the host knows at start hand-off.</summary>
    public void Initialize(Guid gameId, bool isHunter)
    {
        GameId = gameId;
        IsHunter = isHunter;
        TagCommand.RaiseCanExecuteChanged();
    }

    /// <summary>
    /// Subscribes to the shared store, seeds from its current snapshot, reads the first device fix, and starts
    /// the local per-second tick. The store connection itself is started/stopped by the gameplay map VM.
    /// </summary>
    public async Task ActivateAsync()
    {
        _lifecycleCts?.Cancel();
        _lifecycleCts?.Dispose();
        _lifecycleCts = new CancellationTokenSource();

        // Emit the initial follow state so the map starts centred (Center defaults on).
        _mapCamera.SetFollowMode(IsFollowingLocation);

        // Resolve our own id so the penalty regime (a fixed 30s ping cadence) is applied only when the
        // penalty is ours. Cached for the session; a null here just leaves us on the normal cadence.
        _selfUserId = await _currentUser.GetUserIdAsync(_lifecycleCts.Token) ?? _selfUserId;

        Subscribe();
        await RefreshDeviceFixAsync();

        if (_stateService.CurrentState is { } state)
            ApplyState(state);

        _tickTimer ??= _timeProvider.CreateTimer(_ => Tick(), null, OneSecond, OneSecond);
    }

    /// <summary>Unsubscribes and stops ticking; safe to call more than once. Leaves the store connection alone.</summary>
    public void Deactivate()
    {
        _lifecycleCts?.Cancel();
        Unsubscribe();
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

    // Seeds all HUD metrics from one store snapshot: the clock/ping seeds, the preys-active count, the
    // distance inputs, and the game-end signal.
    private void ApplyState(GameLiveState state)
    {
        if (HasEnded)
            return;

        if (state.IsCompleted)
        {
            EndGame();
            return;
        }

        _hunterUserId = state.HunterUserId;
        _participants = state.Participants;
        _hunterDistanceMeters = state.HunterDistanceMeters;
        _penaltyEndsAt = LocalPenaltyEndsAt(state);

        // The game clock and ping cadence are server-authoritative but ticked locally between updates. A
        // real-time delta carries these values frozen from the last full reconcile, so re-seeding on every
        // snapshot would snap both countdowns backward each time a location/participant delta arrives.
        // Re-seed the clock only when the server value actually changed (a genuine reconcile); otherwise
        // keep the local tick running.
        if (!_hasSnapshot || state.GameDurationLeft != _serverGameDurationLeft)
            _gameDurationLeft = Math.Max(0, state.GameDurationLeft);
        _serverGameDurationLeft = state.GameDurationLeft;

        ReseedPingIfChanged(state);

        _preysLeft = state.PreysLeft;
        _totalPreys = CountPreys(state);
        _hasSnapshot = true;

        UpdateCountdownDisplays();
        PreysActiveText = $"{_preysLeft}/{_totalPreys}";
        RecomputeDistance();
    }

    // Re-seeds the next-ping countdown from a snapshot, but only when something that governs the bar has
    // genuinely changed — a fresh server cadence (reconcile) or a flip of our own penalty regime — so the
    // frequent deltas that carry the same values forward don't reset the locally-ticked bar.
    private void ReseedPingIfChanged(GameLiveState state)
    {
        var penalised = IsPenaltyActive();
        var changed = !_pingSeeded
            || penalised != _isPenalised
            || state.NextPingDuration != _serverNextPing
            || state.NextPingDurationWithPenalty != _serverNextPingWithPenalty
            || state.CurrentPingInterval != _serverPingInterval;

        _serverNextPing = state.NextPingDuration;
        _serverNextPingWithPenalty = state.NextPingDurationWithPenalty;
        _serverPingInterval = state.CurrentPingInterval;

        if (changed)
            ApplyPingSeed(penalised);
    }

    // Sets the bar length and the countdown seed for the current regime. Under penalty the bar is a fixed
    // 30 seconds; otherwise it is the server's reporting interval (which shrinks in the endgame). The seed
    // positions us within the current cycle, aligning the local bar with the server-side ping schedule.
    private void ApplyPingSeed(bool penalised)
    {
        _isPenalised = penalised;
        _pingBarMax = penalised
            ? PenaltyBarSeconds
            : (_serverPingInterval > 0 ? _serverPingInterval : PenaltyBarSeconds);

        var seed = penalised ? _serverNextPingWithPenalty : _serverNextPing;
        _nextPingRemaining = seed > 0 ? Math.Min(seed, _pingBarMax) : _pingBarMax;
        _pingSeeded = true;
    }

    /// <summary>Decrements the two local countdowns and loops the ping bar so it restarts after each ping.</summary>
    internal void Tick()
    {
        if (!_hasSnapshot || HasEnded)
            return;

        if (_gameDurationLeft > 0)
            _gameDurationLeft--;

        // The penalty regime can end purely by the clock passing PenaltyEndsAt (no server event needed);
        // when it flips, switch the bar between the 30s penalty cadence and the normal reporting interval.
        var penalised = IsPenaltyActive();
        if (penalised != _isPenalised)
        {
            ApplyPingSeed(penalised);
        }
        else if (_nextPingRemaining > 0)
        {
            _nextPingRemaining--;
        }
        else
        {
            // A ping cycle elapsed with no fresh server seed — the location was just sent, so start the
            // countdown over. The next reconcile re-aligns it to the server schedule.
            _nextPingRemaining = _pingBarMax;
        }

        UpdateCountdownDisplays();

        if (++_ticksSinceGps >= GpsRefreshTicks)
        {
            _ticksSinceGps = 0;
            _ = RefreshDeviceFixAsync();
        }
    }

    // The end of our own active boundary penalty, if any, read from the local participant in the snapshot.
    private DateTimeOffset? LocalPenaltyEndsAt(GameLiveState state)
    {
        foreach (var participant in state.Participants)
        {
            if (participant.UserId == _selfUserId)
                return participant.PenaltyEndsAt;
        }
        return null;
    }

    // True while our boundary penalty is still in effect — the penalty ping cadence (a fixed 30s bar) and
    // the penalty banner then apply. Evaluated against the clock so it self-heals when the penalty simply
    // expires. Named distinctly from the public IsPenalised property, which it feeds.
    private bool IsPenaltyActive() =>
        _penaltyEndsAt is { } endsAt && endsAt > _timeProvider.GetUtcNow();

    // Whole seconds left in our own penalty, rounded up so a penalty ending in 29.4s still reads 00:30
    // rather than skipping a second on the first render. Zero when there is no active penalty.
    private int RemainingPenaltySeconds()
    {
        if (_penaltyEndsAt is not { } endsAt)
            return 0;

        var remaining = endsAt - _timeProvider.GetUtcNow();
        return remaining <= TimeSpan.Zero ? 0 : (int)Math.Ceiling(remaining.TotalSeconds);
    }

    private async Task RefreshDeviceFixAsync()
    {
        try
        {
            _deviceFix = await _gpsReader.ReadAsync(_lifecycleCts?.Token ?? CancellationToken.None);
            RecomputeDistance();
        }
        catch (OperationCanceledException)
        {
            // Deactivated mid-read.
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "HUD device-fix read failed; keeping the last distance.");
        }
    }

    private static int CountPreys(GameLiveState state)
    {
        var count = 0;
        foreach (var participant in state.Participants)
        {
            if (state.HunterUserId is null || participant.UserId != state.HunterUserId.Value)
                count++;
        }
        return count;
    }

    // Called from both paths that can move the displays: every snapshot (ApplyState, after _penaltyEndsAt
    // is re-resolved) and every local tick. Keeping the penalty banner here means it appears and hides on
    // whichever of the two happens first, with no separate wiring.
    private void UpdateCountdownDisplays()
    {
        GameTimeRemainingText = FormatDuration(_gameDurationLeft);
        NextPingRemainingText = FormatDuration(_nextPingRemaining);
        NextPingProgress = _pingBarMax > 0
            ? Math.Clamp((double)_nextPingRemaining / _pingBarMax, 0d, 1d)
            : 0d;

        var penalised = IsPenaltyActive();
        IsPenalised = penalised;
        PenaltyRemainingText = penalised ? FormatDuration(RemainingPenaltySeconds()) : string.Empty;
    }

    private void RecomputeDistance()
    {
        if (IsHunter)
        {
            var nearest = NearestPreyMeters();
            DistanceText = nearest is { } meters ? FormatDistance(meters) : _localization["Hud_Distance_Unknown"];
            return;
        }

        // Prey: prefer a live computation from the hunter's broadcast position (already visible on the map),
        // falling back to the server-computed distance when we lack our own fix or the hunter's location.
        var hunterLocation = HunterLocation();
        if (_deviceFix is { } fix && hunterLocation is { } location)
        {
            DistanceText = FormatDistance(
                GeoDistance.Haversine(fix.Latitude, fix.Longitude, location.Latitude, location.Longitude));
        }
        else
        {
            DistanceText = _hunterDistanceMeters is { } serverMeters
                ? FormatDistance(serverMeters)
                : _localization["Hud_Distance_Unknown"];
        }
    }

    // Nearest still-in-play prey to our own fix (hunter view), or null when none is locatable.
    private double? NearestPreyMeters()
    {
        if (_deviceFix is not { } fix)
            return null;

        double? nearest = null;
        foreach (var participant in _participants)
        {
            if (_hunterUserId is { } hunter && participant.UserId == hunter)
                continue;
            if (GameMapProjection.IsCaught(participant.State))
                continue;
            if (participant.Location is not { } location)
                continue;

            var distance = GeoDistance.Haversine(fix.Latitude, fix.Longitude, location.Latitude, location.Longitude);
            if (nearest is null || distance < nearest)
                nearest = distance;
        }
        return nearest;
    }

    private GpsCoordinate? HunterLocation()
    {
        if (_hunterUserId is not { } hunter)
            return null;
        foreach (var participant in _participants)
        {
            if (participant.UserId == hunter)
                return participant.Location;
        }
        return null;
    }

    private void ToggleCenter()
    {
        IsFollowingLocation = !IsFollowingLocation;
        _mapCamera.SetFollowMode(IsFollowingLocation);
    }

    private void EndGame()
    {
        HasEnded = true;
        Deactivate();
        GameEnded?.Invoke(this, EventArgs.Empty);
    }

    // --- Tag flow (hunter only) ---

    private async Task TagAsync()
    {
        if (!IsHunter)
            return;

        var token = await _accessTokenProvider.GetAccessTokenAsync();
        if (token is null)
        {
            SetError("Hud_Error_Unauthorized");
            return;
        }

        var candidatesResult = await _gameApi.GetTagCandidatesAsync(GameId, token);
        switch (candidatesResult.Outcome)
        {
            case TagCandidatesOutcome.Success:
                break;

            case TagCandidatesOutcome.Unauthorized:
                _accessTokenProvider.Invalidate();
                SetError("Hud_Error_Unauthorized");
                return;

            default:
                // Forbidden / NotFound / Error.
                SetError("Hud_Error_Generic");
                return;
        }

        if (candidatesResult.Candidates.Count == 0)
        {
            SetInfo("Tag_NoPreysInRange");
            return;
        }

        var selectedUserId = await _tagDialog.SelectCandidateAsync(candidatesResult.Candidates);
        if (selectedUserId is null)
            return; // dismissed without choosing

        var confirmed = await _confirmationDialog.ConfirmAsync(
            _localization["Tag_Confirm_Title"],
            _localization["Tag_Confirm_Message"],
            _localization["Tag_Confirm_Accept"],
            _localization["Tag_Confirm_Cancel"]);
        if (!confirmed)
            return; // cancel — no server call

        var tagResult = await _gameApi.TagPlayerAsync(GameId, selectedUserId.Value, token);
        switch (tagResult.Outcome)
        {
            case TagPlayerOutcome.Success:
                // The store broadcasts the resulting status change, so the preys-active count updates itself.
                ClearStatusMessage();
                break;

            case TagPlayerOutcome.Conflict:
            case TagPlayerOutcome.NotFound:
                // Prey moved out of range / already tagged — let the hunter re-open the list.
                SetInfo("Tag_NoLongerInRange");
                break;

            case TagPlayerOutcome.Unauthorized:
                _accessTokenProvider.Invalidate();
                SetError("Hud_Error_Unauthorized");
                break;

            default:
                SetError("Hud_Error_Generic");
                break;
        }
    }

    private void SetError(string key)
    {
        StatusIsError = true;
        StatusMessage = _localization[key];
    }

    private void SetInfo(string key)
    {
        StatusIsError = false;
        StatusMessage = _localization[key];
    }

    private void ClearStatusMessage()
    {
        StatusIsError = false;
        StatusMessage = null;
    }

    private string FormatDistance(double meters)
    {
        if (meters < 1000d)
            return string.Format(_localization["Hud_Distance_Meters"], (int)Math.Round(meters));

        var km = meters / 1000d;
        // Invariant so the decimal separator is stable across device locales.
        return string.Format(_localization["Hud_Distance_Kilometers"], km.ToString("0.0", CultureInfo.InvariantCulture));
    }

    private static string FormatDuration(int totalSeconds)
    {
        if (totalSeconds < 0)
            totalSeconds = 0;

        var span = TimeSpan.FromSeconds(totalSeconds);
        return span.TotalHours >= 1
            ? $"{(int)span.TotalHours}:{span.Minutes:00}:{span.Seconds:00}"
            : $"{span.Minutes:00}:{span.Seconds:00}";
    }

    public void Dispose()
    {
        Deactivate();
        _lifecycleCts?.Dispose();
        _lifecycleCts = null;
    }
}
