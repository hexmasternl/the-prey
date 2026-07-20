using System.Diagnostics;
using HexMaster.ThePrey.Maui.App.Services.Api;
using HexMaster.ThePrey.Maui.App.Services.Authentication;
using HexMaster.ThePrey.Maui.App.Services.Dialogs;
using HexMaster.ThePrey.Maui.App.Services.Localization;
using HexMaster.ThePrey.Maui.App.Services.Location;
using HexMaster.ThePrey.Maui.App.Services.Navigation;
using HexMaster.ThePrey.Maui.App.Services.Realtime;
using HexMaster.ThePrey.Maui.App.Services.Session;
using HexMaster.ThePrey.Maui.App.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace HexMaster.ThePrey.Maui.App.Tests;

public class GameHudViewModelTests
{
    private readonly FakeGameStateService _state = new();
    private readonly Mock<IGameApiClient> _api = new();
    private readonly Mock<IAccessTokenProvider> _tokens = new();
    private readonly Mock<ICurrentUserProvider> _currentUser = new();
    private readonly Mock<IGpsReader> _gps = new();
    private readonly Mock<IMapCameraController> _camera = new();
    private readonly Mock<ITagDialog> _tagDialog = new();
    private readonly Mock<IConfirmationDialog> _confirm = new();
    private readonly Mock<ILocalizationService> _localization = new();
    private readonly FakeTimeProvider _time = new();
    private readonly Guid _gameId = Guid.NewGuid();
    private readonly Guid _hunterId = Guid.NewGuid();

    public GameHudViewModelTests()
    {
        _localization.Setup(l => l[It.IsAny<string>()]).Returns((string key) => key switch
        {
            "Hud_Distance_Meters" => "{0} m",
            "Hud_Distance_Kilometers" => "{0} km",
            "Hud_Distance_Unknown" => "—",
            _ => key
        });
    }

    private GameHudViewModel CreateSut(bool isHunter)
    {
        var vm = new GameHudViewModel(
            _state, _api.Object, _tokens.Object, _currentUser.Object, _gps.Object, _camera.Object,
            _tagDialog.Object, _confirm.Object, _localization.Object, _time,
            NullLogger<GameHudViewModel>.Instance);
        vm.Initialize(_gameId, isHunter);
        return vm;
    }

    private void SetupSelf(Guid selfUserId) =>
        _currentUser.Setup(c => c.GetUserIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(selfUserId);

    private void SetupToken(string? token = "token") =>
        _tokens.Setup(t => t.GetAccessTokenAsync(It.IsAny<CancellationToken>())).ReturnsAsync(token);

    private GameLiveState State(
        string status = "InProgress", int duration = 100, int nextPing = 30, int interval = 60,
        int nextPingWithPenalty = 0, int preysLeft = 1, int? hunterDistance = null,
        params GameLiveParticipant[] participants) =>
        new()
        {
            GameId = _gameId,
            Status = status,
            HunterUserId = _hunterId,
            Participants = participants,
            PlayfieldCoordinates = Array.Empty<GpsCoordinate>(),
            GameDurationLeft = duration,
            NextPingDuration = nextPing,
            CurrentPingInterval = interval,
            NextPingDurationWithPenalty = nextPingWithPenalty,
            PreysLeft = preysLeft,
            HunterDistanceMeters = hunterDistance,
        };

    private static GameLiveParticipant P(
        Guid id, string state = "Active", GpsCoordinate? location = null, DateTimeOffset? penaltyEndsAt = null) =>
        new(id, state, location, penaltyEndsAt);

    private static async Task WaitFor(Func<bool> condition, string because)
    {
        var sw = Stopwatch.StartNew();
        while (!condition() && sw.ElapsedMilliseconds < 3000)
            await Task.Delay(10);
        Assert.True(condition(), because);
    }

    // ---- Metrics ----

    [Fact]
    public async Task Metrics_PreysActiveOverTotal_ExcludesHunterFromDenominator()
    {
        using var sut = CreateSut(isHunter: false);
        await sut.ActivateAsync();

        _state.Push(State(preysLeft: 1, participants: new[]
        {
            P(_hunterId), P(Guid.NewGuid()), P(Guid.NewGuid()), // hunter + 2 preys
        }));

        Assert.Equal("1/2", sut.PreysActiveText);
    }

    [Fact]
    public async Task Metrics_PreyDistance_UsesServerDistance_WhenHunterLocationUnknown()
    {
        using var sut = CreateSut(isHunter: false);
        await sut.ActivateAsync();

        // No hunter location in the snapshot → fall back to the server-computed distance.
        _state.Push(State(hunterDistance: 250, participants: new[] { P(Guid.NewGuid()) }));

        Assert.Equal("250 m", sut.DistanceText);
    }

    [Fact]
    public async Task Metrics_HunterDistance_ComputesNearestFromPreyLocations()
    {
        _gps.Setup(g => g.ReadAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new GpsFix(0, 0));
        using var sut = CreateSut(isHunter: true);
        await sut.ActivateAsync();

        _state.Push(State(participants: new[]
        {
            P(Guid.NewGuid(), "Active", new GpsCoordinate(0, 1)),
            P(Guid.NewGuid(), "Active", new GpsCoordinate(0, 0.5)),
        }));

        // Nearest is the 0.5° point (~55.6 km), not the 1° point.
        Assert.Equal("55.6 km", sut.DistanceText);
    }

    [Fact]
    public async Task Metrics_PreyDistance_ShowsUnknown_WhenServerDistanceNull()
    {
        using var sut = CreateSut(isHunter: false);
        await sut.ActivateAsync();

        _state.Push(State(hunterDistance: null, participants: new[] { P(Guid.NewGuid()) }));

        Assert.Equal("—", sut.DistanceText);
    }

    [Fact]
    public async Task Metrics_HunterDistance_ShowsUnknown_WhenNoDeviceFix()
    {
        _gps.Setup(g => g.ReadAsync(It.IsAny<CancellationToken>())).ReturnsAsync((GpsFix?)null);
        using var sut = CreateSut(isHunter: true);
        await sut.ActivateAsync();

        _state.Push(State(participants: new[] { P(Guid.NewGuid(), "Active", new GpsCoordinate(0, 0.5)) }));

        Assert.Equal("—", sut.DistanceText);
    }

    // ---- Countdown ----

    [Fact]
    public async Task Countdown_TicksDownEachSecond_AndReSeedsFromSnapshot()
    {
        using var sut = CreateSut(isHunter: false);
        await sut.ActivateAsync();

        _state.Push(State(duration: 100, nextPing: 30, interval: 60));
        Assert.Equal("01:40", sut.GameTimeRemainingText);
        Assert.Equal(0.5, sut.NextPingProgress, 5);

        _time.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal("01:39", sut.GameTimeRemainingText);
        Assert.Equal(29d / 60d, sut.NextPingProgress, 5);

        // A fresh snapshot re-seeds and corrects drift.
        _state.Push(State(duration: 200, nextPing: 60, interval: 60));
        Assert.Equal("03:20", sut.GameTimeRemainingText);
        Assert.Equal(1.0, sut.NextPingProgress, 5);
    }

    [Fact]
    public async Task Countdown_LoopsBackToInterval_WhenTheNextPingElapses()
    {
        using var sut = CreateSut(isHunter: false);
        await sut.ActivateAsync();

        _state.Push(State(nextPing: 2, interval: 60));

        // Ticks 2 → 1 → 0 (the ping fires).
        _time.Advance(TimeSpan.FromSeconds(2));
        Assert.Equal(0d, sut.NextPingProgress, 5);

        // The next tick starts the countdown over at the full interval instead of sticking at zero.
        _time.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(1.0, sut.NextPingProgress, 5);
    }

    [Fact]
    public async Task Countdown_DoesNotSnapBack_WhenADeltaRepeatsTheServerValues()
    {
        using var sut = CreateSut(isHunter: false);
        await sut.ActivateAsync();

        _state.Push(State(duration: 100, nextPing: 30, interval: 60));
        _time.Advance(TimeSpan.FromSeconds(5));
        Assert.Equal(25d / 60d, sut.NextPingProgress, 5);
        Assert.Equal("01:35", sut.GameTimeRemainingText);

        // A real-time delta carries the same clock/ping values forward from the last reconcile — it must
        // not reset the locally-ticked countdowns.
        _state.Push(State(duration: 100, nextPing: 30, interval: 60));
        Assert.Equal(25d / 60d, sut.NextPingProgress, 5);
        Assert.Equal("01:35", sut.GameTimeRemainingText);
    }

    [Fact]
    public async Task Countdown_UsesThirtySecondPenaltyBar_WhenLocalPlayerIsPenalised()
    {
        var self = Guid.NewGuid();
        SetupSelf(self);
        using var sut = CreateSut(isHunter: false);
        await sut.ActivateAsync();

        var penaltyEnds = _time.GetUtcNow() + TimeSpan.FromMinutes(1);
        _state.Push(State(nextPing: 10, interval: 60, nextPingWithPenalty: 20,
            participants: new[] { P(self, penaltyEndsAt: penaltyEnds) }));

        // Bar length is the fixed 30s penalty cadence (not the 60s interval), seeded from the penalty value.
        Assert.Equal(20d / 30d, sut.NextPingProgress, 5);
    }

    [Fact]
    public async Task Countdown_RevertsToNormalInterval_WhenThePenaltyExpiresLocally()
    {
        var self = Guid.NewGuid();
        SetupSelf(self);
        using var sut = CreateSut(isHunter: false);
        await sut.ActivateAsync();

        var penaltyEnds = _time.GetUtcNow() + TimeSpan.FromSeconds(1);
        _state.Push(State(nextPing: 40, interval: 60, nextPingWithPenalty: 25,
            participants: new[] { P(self, penaltyEndsAt: penaltyEnds) }));
        Assert.Equal(25d / 30d, sut.NextPingProgress, 5); // penalised: 30s bar

        // The tick on which the clock passes the penalty end flips the regime back to the normal reporting
        // interval (60s here) and re-seeds from the normal cadence — with no server event required.
        _time.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(40d / 60d, sut.NextPingProgress, 5);
    }

    // ---- Penalty banner ----

    [Fact]
    public async Task PenaltyBanner_IsShownWithCountdown_WhenLocalPlayerIsPenalised()
    {
        var self = Guid.NewGuid();
        SetupSelf(self);
        using var sut = CreateSut(isHunter: false);
        await sut.ActivateAsync();

        var penaltyEnds = _time.GetUtcNow() + TimeSpan.FromSeconds(90);
        _state.Push(State(participants: new[] { P(self, penaltyEndsAt: penaltyEnds) }));

        Assert.True(sut.IsPenalised);
        Assert.Equal("01:30", sut.PenaltyRemainingText);
    }

    [Fact]
    public async Task PenaltyBanner_CountdownDecreases_AsTheClockAdvances()
    {
        var self = Guid.NewGuid();
        SetupSelf(self);
        using var sut = CreateSut(isHunter: false);
        await sut.ActivateAsync();

        _state.Push(State(participants: new[]
        {
            P(self, penaltyEndsAt: _time.GetUtcNow() + TimeSpan.FromSeconds(30)),
        }));
        Assert.Equal("00:30", sut.PenaltyRemainingText);

        _time.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal("00:29", sut.PenaltyRemainingText);

        _time.Advance(TimeSpan.FromSeconds(9));
        Assert.Equal("00:20", sut.PenaltyRemainingText);
        Assert.True(sut.IsPenalised);
    }

    [Fact]
    public async Task PenaltyBanner_Hides_WhenTheClockPassesPenaltyEnd_WithoutAnySnapshot()
    {
        var self = Guid.NewGuid();
        SetupSelf(self);
        using var sut = CreateSut(isHunter: false);
        await sut.ActivateAsync();

        _state.Push(State(participants: new[]
        {
            P(self, penaltyEndsAt: _time.GetUtcNow() + TimeSpan.FromSeconds(2)),
        }));
        Assert.True(sut.IsPenalised);

        // Purely clock-driven — no further store snapshot is pushed.
        _time.Advance(TimeSpan.FromSeconds(2));

        Assert.False(sut.IsPenalised);
        Assert.Equal(string.Empty, sut.PenaltyRemainingText);
    }

    [Fact]
    public async Task PenaltyBanner_IsHidden_WhenTheLocalPlayerHasNoPenalty()
    {
        var self = Guid.NewGuid();
        SetupSelf(self);
        using var sut = CreateSut(isHunter: false);
        await sut.ActivateAsync();

        _state.Push(State(participants: new[] { P(self) }));

        Assert.False(sut.IsPenalised);
        Assert.Equal(string.Empty, sut.PenaltyRemainingText);
    }

    [Fact]
    public async Task PenaltyBanner_IsHidden_WhenOnlyAnotherParticipantIsPenalised()
    {
        var self = Guid.NewGuid();
        SetupSelf(self);
        using var sut = CreateSut(isHunter: false);
        await sut.ActivateAsync();

        // Someone else's penalty must never surface on our screen.
        _state.Push(State(participants: new[]
        {
            P(self),
            P(Guid.NewGuid(), penaltyEndsAt: _time.GetUtcNow() + TimeSpan.FromMinutes(5)),
        }));

        Assert.False(sut.IsPenalised);
        Assert.Equal(string.Empty, sut.PenaltyRemainingText);
    }

    // The banner is role-agnostic: a penalised hunter sees it exactly as a penalised prey does.
    [Fact]
    public async Task PenaltyBanner_IsShown_ForAPenalisedHunter()
    {
        SetupSelf(_hunterId);
        using var sut = CreateSut(isHunter: true);
        await sut.ActivateAsync();

        _state.Push(State(participants: new[]
        {
            P(_hunterId, penaltyEndsAt: _time.GetUtcNow() + TimeSpan.FromSeconds(45)),
        }));

        Assert.True(sut.IsPenalised);
        Assert.Equal("00:45", sut.PenaltyRemainingText);
    }

    [Fact]
    public async Task Seed_PopulatesMetrics()
    {
        using var sut = CreateSut(isHunter: false);
        await sut.ActivateAsync();

        _state.Push(State(duration: 65));

        Assert.Equal("01:05", sut.GameTimeRemainingText);
    }

    [Fact]
    public async Task GameCompleted_StopsTickingAndSignalsHost()
    {
        using var sut = CreateSut(isHunter: false);
        var ended = false;
        sut.GameEnded += (_, _) => ended = true;

        await sut.ActivateAsync();
        _state.Push(State(duration: 100));
        _state.Push(State(status: "Completed"));

        Assert.True(sut.HasEnded);
        Assert.True(ended);

        // Ticking has stopped — advancing time no longer changes the clock.
        var frozen = sut.GameTimeRemainingText;
        _time.Advance(TimeSpan.FromSeconds(3));
        Assert.Equal(frozen, sut.GameTimeRemainingText);
    }

    // ---- Center toggle ----

    [Fact]
    public void CenterToggle_FlipsFollowState_AndSignalsMap()
    {
        var sut = CreateSut(isHunter: false);
        Assert.True(sut.IsFollowingLocation);

        sut.ToggleCenterCommand.Execute(null);
        Assert.False(sut.IsFollowingLocation);
        _camera.Verify(c => c.SetFollowMode(false), Times.Once);

        sut.ToggleCenterCommand.Execute(null);
        Assert.True(sut.IsFollowingLocation);
        _camera.Verify(c => c.SetFollowMode(true), Times.Once);
    }

    // ---- Tag flow ----

    [Fact]
    public void Tag_IsHiddenForPreys()
    {
        var sut = CreateSut(isHunter: false);

        Assert.False(sut.TagCommand.CanExecute(null));
    }

    [Fact]
    public async Task Tag_HunterHappyPath_SelectsConfirmsAndTags()
    {
        var prey = Guid.NewGuid();
        SetupToken();
        _api.Setup(a => a.GetTagCandidatesAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TagCandidatesResult.Success(new[] { new TagCandidate(prey, "GHOST", 10, "Active") }, 30));
        _tagDialog.Setup(d => d.SelectCandidateAsync(It.IsAny<IReadOnlyList<TagCandidate>>())).ReturnsAsync(prey);
        _confirm.Setup(c => c.ConfirmAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);
        Guid? tagged = null;
        _api.Setup(a => a.TagPlayerAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback((Guid _, Guid participant, string _, CancellationToken _) => tagged = participant)
            .ReturnsAsync(TagPlayerResult.Success);
        var sut = CreateSut(isHunter: true);

        sut.TagCommand.Execute(null);

        await WaitFor(() => tagged == prey, "the selected prey is tagged");
    }

    [Fact]
    public async Task Tag_NoCandidates_ShowsMessageAndOpensNoDialog()
    {
        SetupToken();
        _api.Setup(a => a.GetTagCandidatesAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TagCandidatesResult.Success(Array.Empty<TagCandidate>(), 30));
        var sut = CreateSut(isHunter: true);

        sut.TagCommand.Execute(null);

        await WaitFor(() => sut.HasStatusMessage, "a no-preys message is shown");
        Assert.Equal("Tag_NoPreysInRange", sut.StatusMessage);
        Assert.False(sut.StatusIsError);
        _tagDialog.Verify(d => d.SelectCandidateAsync(It.IsAny<IReadOnlyList<TagCandidate>>()), Times.Never);
        _api.Verify(
            a => a.TagPlayerAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Tag_CancelConfirmation_MakesNoTagCall()
    {
        var prey = Guid.NewGuid();
        SetupToken();
        _api.Setup(a => a.GetTagCandidatesAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TagCandidatesResult.Success(new[] { new TagCandidate(prey, "GHOST", 10, "Active") }, 30));
        _tagDialog.Setup(d => d.SelectCandidateAsync(It.IsAny<IReadOnlyList<TagCandidate>>())).ReturnsAsync(prey);
        var confirmShown = false;
        _confirm.Setup(c => c.ConfirmAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Callback(() => confirmShown = true)
            .ReturnsAsync(false);
        var sut = CreateSut(isHunter: true);

        sut.TagCommand.Execute(null);

        await WaitFor(() => confirmShown, "the confirmation prompt is shown");
        _api.Verify(
            a => a.TagPlayerAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Tag_NoLongerTaggable_ShowsMessageAndAllowsReopen()
    {
        var prey = Guid.NewGuid();
        SetupToken();
        _api.Setup(a => a.GetTagCandidatesAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TagCandidatesResult.Success(new[] { new TagCandidate(prey, "GHOST", 10, "Active") }, 30));
        _tagDialog.Setup(d => d.SelectCandidateAsync(It.IsAny<IReadOnlyList<TagCandidate>>())).ReturnsAsync(prey);
        _confirm.Setup(c => c.ConfirmAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);
        _api.Setup(a => a.TagPlayerAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TagPlayerResult.Conflict);
        var sut = CreateSut(isHunter: true);

        sut.TagCommand.Execute(null);

        await WaitFor(() => sut.HasStatusMessage, "a no-longer-in-range message is shown");
        Assert.Equal("Tag_NoLongerInRange", sut.StatusMessage);
        Assert.False(sut.StatusIsError);
        Assert.True(sut.TagCommand.CanExecute(null)); // hunter may re-open the list
    }
}
