using System.Windows.Input;
using HexMaster.ThePrey.Maui.App.Services.Api;
using HexMaster.ThePrey.Maui.App.Services.Authentication;
using HexMaster.ThePrey.Maui.App.Services.Dialogs;
using HexMaster.ThePrey.Maui.App.Services.Localization;
using HexMaster.ThePrey.Maui.App.Services.Location;
using HexMaster.ThePrey.Maui.App.Services.Navigation;
using Microsoft.Extensions.Logging;

namespace HexMaster.ThePrey.Maui.App.ViewModels;

/// <summary>
/// Drives the in-game HUD overlay hosted by the (separately-owned) gameplay map page. Projects the
/// game clock, next-ping countdown, preys-active count and the role-aware nearest-adversary distance;
/// ticks the two countdowns locally each second seeded from the server and re-synced on every poll;
/// polls <c>GET …/status</c> (+ <c>…/state</c>) on a ping-anchored cadence; owns the collapse/expand
/// and Center follow toggle; and orchestrates the hunter's Tag flow. All HTTP, GPS, dialogs, the map
/// signal, and time sit behind interfaces / <see cref="TimeProvider"/> so the whole VM is unit-testable.
/// </summary>
public sealed class GameHudViewModel : ObservableObject, IDisposable
{
    /// <summary>Never poll more often than this, even if the server reports a very short next ping.</summary>
    public static readonly TimeSpan MinimumRefreshInterval = TimeSpan.FromSeconds(5);

    /// <summary>Never let the poll cadence drift longer than this, so the metrics stay reasonably fresh.</summary>
    public static readonly TimeSpan MaximumRefreshInterval = TimeSpan.FromSeconds(30);

    private static readonly TimeSpan OneSecond = TimeSpan.FromSeconds(1);

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
    private ITimer? _refreshTimer;
    private CancellationTokenSource? _lifecycleCts;

    // Seed values from the latest snapshot, ticked locally between polls.
    private int _gameDurationLeft;
    private int _nextPingRemaining;
    private int _currentPingInterval;
    private int _preysLeft;
    private int _totalPreys;
    private bool _hasSnapshot;

    // Role-specific distance inputs.
    private int? _hunterDistanceMeters;                 // prey view (server-computed)
    private IReadOnlyList<GpsCoordinate> _preyLocations = Array.Empty<GpsCoordinate>(); // hunter view
    private GpsFix? _deviceFix;                          // hunter view

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

    /// <summary>Raised once, when a status refresh reports the game is completed, so the host can hand off.</summary>
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

    /// <summary>Refreshes immediately, then starts the local tick and the periodic refresh cadence.</summary>
    public async Task ActivateAsync()
    {
        _lifecycleCts?.Cancel();
        _lifecycleCts?.Dispose();
        _lifecycleCts = new CancellationTokenSource();

        // Emit the initial follow state so the map starts centred (Center defaults on).
        _mapCamera.SetFollowMode(IsFollowingLocation);

        await RefreshAsync(_lifecycleCts.Token);

        _tickTimer ??= _timeProvider.CreateTimer(_ => Tick(), null, OneSecond, OneSecond);
    }

    /// <summary>Stops ticking and polling; safe to call more than once.</summary>
    public void Deactivate()
    {
        _lifecycleCts?.Cancel();
        _tickTimer?.Dispose();
        _tickTimer = null;
        _refreshTimer?.Dispose();
        _refreshTimer = null;
    }

    /// <summary>
    /// Acquires a token and polls the game status (and role-specific state), mapping every outcome:
    /// a completed game stops the HUD and signals the host; unauthorized invalidates the token; a
    /// transient failure keeps the last known values on screen.
    /// </summary>
    public async Task RefreshAsync(CancellationToken ct = default)
    {
        if (HasEnded)
            return;

        var token = await _accessTokenProvider.GetAccessTokenAsync(ct);
        if (token is null)
        {
            SetError("Hud_Error_Unauthorized");
            return;
        }

        var statusResult = await _gameApi.GetGameStatusAsync(GameId, token, ct);
        switch (statusResult.Outcome)
        {
            case GameStatusOutcome.Success:
                SeedFromStatus(statusResult.Status!);
                ClearStatusMessage();
                break;

            case GameStatusOutcome.Completed:
                EndGame();
                return;

            case GameStatusOutcome.Unauthorized:
                _accessTokenProvider.Invalidate();
                SetError("Hud_Error_Unauthorized");
                return;

            default:
                // NotFound / Forbidden / Error — transient; keep last values and retry next cadence.
                _logger.LogWarning("Game-status refresh returned {Outcome}; keeping last values.", statusResult.Outcome);
                if (!_hasSnapshot)
                    SetError("Hud_Error_Generic");
                ScheduleNextRefresh();
                return;
        }

        // The distance metric needs the role-specific state for both roles.
        var stateResult = await _gameApi.GetGameStateAsync(GameId, token, ct);
        switch (stateResult.Outcome)
        {
            case GameStateOutcome.Success:
                _hunterDistanceMeters = stateResult.State!.HunterDistanceMeters;
                _preyLocations = stateResult.State.PreyLocations ?? Array.Empty<GpsCoordinate>();
                if (IsHunter)
                    _deviceFix = await _gpsReader.ReadAsync(ct);
                RecomputeDistance();
                break;

            case GameStateOutcome.Unauthorized:
                _accessTokenProvider.Invalidate();
                SetError("Hud_Error_Unauthorized");
                break;

            default:
                _logger.LogWarning("Game-state refresh returned {Outcome}; keeping last distance.", stateResult.Outcome);
                break;
        }

        ScheduleNextRefresh();
    }

    /// <summary>Decrements the two local countdowns toward zero and refreshes their bound text/progress.</summary>
    internal void Tick()
    {
        if (!_hasSnapshot || HasEnded)
            return;

        if (_gameDurationLeft > 0)
            _gameDurationLeft--;
        if (_nextPingRemaining > 0)
            _nextPingRemaining--;

        UpdateCountdownDisplays();
    }

    private void SeedFromStatus(GameStatusSnapshot status)
    {
        _gameDurationLeft = Math.Max(0, status.GameDurationLeft);
        _nextPingRemaining = Math.Max(0, status.NextPingDuration);
        _currentPingInterval = status.CurrentPingInterval;
        _preysLeft = status.PreysLeft;
        _totalPreys = CountPreys(status);
        _hasSnapshot = true;

        UpdateCountdownDisplays();
        PreysActiveText = $"{_preysLeft}/{_totalPreys}";
    }

    private static int CountPreys(GameStatusSnapshot status)
    {
        // Total preys = participants excluding the hunter.
        var count = 0;
        foreach (var participant in status.Participants)
        {
            if (status.HunterUserId is null || participant.UserId != status.HunterUserId.Value)
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
            if (_deviceFix is null || _preyLocations.Count == 0)
            {
                DistanceText = _localization["Hud_Distance_Unknown"];
                return;
            }

            var nearest = double.MaxValue;
            foreach (var prey in _preyLocations)
            {
                var d = GeoDistance.Haversine(_deviceFix.Latitude, _deviceFix.Longitude, prey.Latitude, prey.Longitude);
                if (d < nearest)
                    nearest = d;
            }
            DistanceText = FormatDistance(nearest);
        }
        else
        {
            DistanceText = _hunterDistanceMeters is { } meters
                ? FormatDistance(meters)
                : _localization["Hud_Distance_Unknown"];
        }
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

    private void ScheduleNextRefresh()
    {
        if (HasEnded || _lifecycleCts is null)
            return;

        var seconds = _currentPingInterval > 0 ? _currentPingInterval : (int)MaximumRefreshInterval.TotalSeconds;
        var next = TimeSpan.FromSeconds(seconds);
        if (next < MinimumRefreshInterval) next = MinimumRefreshInterval;
        if (next > MaximumRefreshInterval) next = MaximumRefreshInterval;

        _refreshTimer ??= _timeProvider.CreateTimer(_ => _ = SafeRefreshAsync(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        _refreshTimer.Change(next, Timeout.InfiniteTimeSpan);
    }

    private async Task SafeRefreshAsync()
    {
        var ct = _lifecycleCts?.Token ?? CancellationToken.None;
        try
        {
            await RefreshAsync(ct);
        }
        catch (OperationCanceledException)
        {
            // Deactivated mid-refresh; nothing to do.
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Scheduled HUD refresh failed.");
        }
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
                ClearStatusMessage();
                // Reflect the reduced preys promptly rather than waiting for the next cadence.
                await RefreshAsync(_lifecycleCts?.Token ?? CancellationToken.None);
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
        return string.Format(_localization["Hud_Distance_Kilometers"], km.ToString("0.0"));
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
