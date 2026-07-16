using System.Globalization;
using System.Windows.Input;
using HexMaster.ThePrey.Maui.App.Services.Api;
using HexMaster.ThePrey.Maui.App.Services.Authentication;
using HexMaster.ThePrey.Maui.App.Services.Dialogs;
using HexMaster.ThePrey.Maui.App.Services.Localization;
using HexMaster.ThePrey.Maui.App.Services.Location;
using HexMaster.ThePrey.Maui.App.Services.Navigation;
using HexMaster.ThePrey.Maui.App.Services.Realtime;
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

    private readonly IGameStateService _stateService;
    private readonly IGameApiClient _gameApi;
    private readonly IAccessTokenProvider _accessTokenProvider;
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

    // Seed values from the latest snapshot, ticked locally between updates.
    private int _gameDurationLeft;
    private int _nextPingRemaining;
    private int _currentPingInterval;
    private int _preysLeft;
    private int _totalPreys;
    private bool _hasSnapshot;

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
    private string? _statusMessage;
    private bool _statusIsError;

    public GameHudViewModel(
        IGameStateService stateService,
        IGameApiClient gameApi,
        IAccessTokenProvider accessTokenProvider,
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

        _gameDurationLeft = Math.Max(0, state.GameDurationLeft);
        _nextPingRemaining = Math.Max(0, state.NextPingDuration);
        _currentPingInterval = state.CurrentPingInterval;
        _preysLeft = state.PreysLeft;
        _totalPreys = CountPreys(state);
        _hasSnapshot = true;

        UpdateCountdownDisplays();
        PreysActiveText = $"{_preysLeft}/{_totalPreys}";
        RecomputeDistance();
    }

    /// <summary>Decrements the two local countdowns toward zero and periodically re-reads the device fix.</summary>
    internal void Tick()
    {
        if (!_hasSnapshot || HasEnded)
            return;

        if (_gameDurationLeft > 0)
            _gameDurationLeft--;
        if (_nextPingRemaining > 0)
            _nextPingRemaining--;

        UpdateCountdownDisplays();

        if (++_ticksSinceGps >= GpsRefreshTicks)
        {
            _ticksSinceGps = 0;
            _ = RefreshDeviceFixAsync();
        }
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

    private void UpdateCountdownDisplays()
    {
        GameTimeRemainingText = FormatDuration(_gameDurationLeft);
        NextPingRemainingText = FormatDuration(_nextPingRemaining);
        NextPingProgress = _currentPingInterval > 0
            ? Math.Clamp((double)_nextPingRemaining / _currentPingInterval, 0d, 1d)
            : 0d;
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
