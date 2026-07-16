using System.Diagnostics;
using HexMaster.ThePrey.Maui.App.Services.Api;
using HexMaster.ThePrey.Maui.App.Services.Authentication;
using HexMaster.ThePrey.Maui.App.Services.Dialogs;
using HexMaster.ThePrey.Maui.App.Services.Localization;
using HexMaster.ThePrey.Maui.App.Services.Location;
using HexMaster.ThePrey.Maui.App.Services.Navigation;
using HexMaster.ThePrey.Maui.App.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace HexMaster.ThePrey.Maui.App.Tests;

public class GameHudViewModelTests
{
    private readonly Mock<IGameApiClient> _api = new();
    private readonly Mock<IAccessTokenProvider> _tokens = new();
    private readonly Mock<IGpsReader> _gps = new();
    private readonly Mock<IMapCameraController> _camera = new();
    private readonly Mock<ITagDialog> _tagDialog = new();
    private readonly Mock<IConfirmationDialog> _confirm = new();
    private readonly Mock<ILocalizationService> _localization = new();
    private readonly FakeTimeProvider _time = new();
    private readonly Guid _gameId = Guid.NewGuid();

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
            _api.Object, _tokens.Object, _gps.Object, _camera.Object, _tagDialog.Object,
            _confirm.Object, _localization.Object, _time, NullLogger<GameHudViewModel>.Instance);
        vm.Initialize(_gameId, isHunter);
        return vm;
    }

    private void SetupToken(string? token = "token") =>
        _tokens.Setup(t => t.GetAccessTokenAsync(It.IsAny<CancellationToken>())).ReturnsAsync(token);

    private void SetupStatus(GameStatusResult result) =>
        _api.Setup(a => a.GetGameStatusAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

    private void SetupState(GameStateResult result) =>
        _api.Setup(a => a.GetGameStateAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

    private static GameStatusSnapshot Status(
        int duration = 100, int nextPing = 30, int interval = 60, int preysLeft = 1, Guid? hunter = null, int preys = 1)
    {
        var hunterId = hunter ?? Guid.NewGuid();
        var participants = new List<GameParticipantSnapshot> { new(hunterId) };
        for (var i = 0; i < preys; i++)
            participants.Add(new GameParticipantSnapshot(Guid.NewGuid()));
        return new GameStatusSnapshot(duration, nextPing, interval, false, preysLeft, hunterId, participants);
    }

    private static GameStateSnapshot EmptyState => new(null, Array.Empty<GpsCoordinate>());

    private static async Task WaitFor(Func<bool> condition, string because)
    {
        var sw = Stopwatch.StartNew();
        while (!condition() && sw.ElapsedMilliseconds < 3000)
            await Task.Delay(10);
        Assert.True(condition(), because);
    }

    // ---- Metrics (9.3) ----

    [Fact]
    public async Task Metrics_PreysActiveOverTotal_ExcludesHunterFromDenominator()
    {
        SetupToken();
        SetupStatus(GameStatusResult.Success(Status(preysLeft: 1, preys: 2))); // hunter + 2 preys
        SetupState(GameStateResult.Success(EmptyState));
        var sut = CreateSut(isHunter: false);

        await sut.RefreshAsync();

        Assert.Equal("1/2", sut.PreysActiveText);
    }

    [Fact]
    public async Task Metrics_PreyDistance_UsesServerDistance()
    {
        SetupToken();
        SetupStatus(GameStatusResult.Success(Status()));
        SetupState(GameStateResult.Success(new GameStateSnapshot(250, Array.Empty<GpsCoordinate>())));
        var sut = CreateSut(isHunter: false);

        await sut.RefreshAsync();

        Assert.Equal("250 m", sut.DistanceText);
    }

    [Fact]
    public async Task Metrics_HunterDistance_ComputesNearestFromPreyLocations()
    {
        SetupToken();
        SetupStatus(GameStatusResult.Success(Status()));
        SetupState(GameStateResult.Success(new GameStateSnapshot(
            null, new[] { new GpsCoordinate(0, 1), new GpsCoordinate(0, 0.5) })));
        _gps.Setup(g => g.ReadAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new GpsFix(0, 0));
        var sut = CreateSut(isHunter: true);

        await sut.RefreshAsync();

        // Nearest is the 0.5° point (~55.6 km), not the 1° point.
        Assert.Equal("55.6 km", sut.DistanceText);
    }

    [Fact]
    public async Task Metrics_PreyDistance_ShowsUnknown_WhenServerDistanceNull()
    {
        SetupToken();
        SetupStatus(GameStatusResult.Success(Status()));
        SetupState(GameStateResult.Success(EmptyState));
        var sut = CreateSut(isHunter: false);

        await sut.RefreshAsync();

        Assert.Equal("—", sut.DistanceText);
    }

    [Fact]
    public async Task Metrics_HunterDistance_ShowsUnknown_WhenNoDeviceFix()
    {
        SetupToken();
        SetupStatus(GameStatusResult.Success(Status()));
        SetupState(GameStateResult.Success(new GameStateSnapshot(null, new[] { new GpsCoordinate(0, 0.5) })));
        _gps.Setup(g => g.ReadAsync(It.IsAny<CancellationToken>())).ReturnsAsync((GpsFix?)null);
        var sut = CreateSut(isHunter: true);

        await sut.RefreshAsync();

        Assert.Equal("—", sut.DistanceText);
    }

    // ---- Countdown (9.4) ----

    [Fact]
    public async Task Countdown_TicksDownEachSecond_AndReSeedsFromSnapshot()
    {
        SetupToken();
        SetupStatus(GameStatusResult.Success(Status(duration: 100, nextPing: 30, interval: 60)));
        SetupState(GameStateResult.Success(EmptyState));
        using var sut = CreateSut(isHunter: false);

        await sut.ActivateAsync();
        Assert.Equal("01:40", sut.GameTimeRemainingText);
        Assert.Equal(0.5, sut.NextPingProgress, 5);

        _time.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal("01:39", sut.GameTimeRemainingText);
        Assert.Equal(29d / 60d, sut.NextPingProgress, 5);

        // A fresh snapshot re-seeds and corrects drift.
        SetupStatus(GameStatusResult.Success(Status(duration: 200, nextPing: 60, interval: 60)));
        await sut.RefreshAsync();
        Assert.Equal("03:20", sut.GameTimeRemainingText);
        Assert.Equal(1.0, sut.NextPingProgress, 5);
    }

    // ---- Refresh (9.5) ----

    [Fact]
    public async Task Refresh_InitialLoad_PopulatesMetrics()
    {
        SetupToken();
        SetupStatus(GameStatusResult.Success(Status(duration: 65)));
        SetupState(GameStateResult.Success(EmptyState));
        var sut = CreateSut(isHunter: false);

        await sut.RefreshAsync();

        Assert.Equal("01:05", sut.GameTimeRemainingText);
    }

    [Fact]
    public async Task Refresh_Completed_StopsTickingAndSignalsHost()
    {
        SetupToken();
        SetupStatus(GameStatusResult.Success(Status(duration: 100)));
        SetupState(GameStateResult.Success(EmptyState));
        using var sut = CreateSut(isHunter: false);
        var ended = false;
        sut.GameEnded += (_, _) => ended = true;

        await sut.ActivateAsync();
        SetupStatus(GameStatusResult.Completed);
        await sut.RefreshAsync();

        Assert.True(sut.HasEnded);
        Assert.True(ended);

        // Ticking has stopped — advancing time no longer changes the clock.
        var frozen = sut.GameTimeRemainingText;
        _time.Advance(TimeSpan.FromSeconds(3));
        Assert.Equal(frozen, sut.GameTimeRemainingText);
    }

    [Fact]
    public async Task Refresh_Unauthorized_InvalidatesTokenAndSurfacesError()
    {
        SetupToken();
        SetupStatus(GameStatusResult.Unauthorized);
        var sut = CreateSut(isHunter: false);

        await sut.RefreshAsync();

        _tokens.Verify(t => t.Invalidate(), Times.Once);
        Assert.True(sut.StatusIsError);
        Assert.True(sut.HasStatusMessage);
    }

    [Fact]
    public async Task Refresh_TransientFailure_KeepsLastKnownValues()
    {
        SetupToken();
        SetupStatus(GameStatusResult.Success(Status(duration: 100)));
        SetupState(GameStateResult.Success(EmptyState));
        var sut = CreateSut(isHunter: false);
        await sut.RefreshAsync();
        var lastClock = sut.GameTimeRemainingText;

        SetupStatus(GameStatusResult.Error);
        await sut.RefreshAsync();

        Assert.Equal(lastClock, sut.GameTimeRemainingText);
    }

    // ---- Center toggle (9.6) ----

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

    // ---- Tag flow (9.7) ----

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
        SetupStatus(GameStatusResult.Success(Status()));
        SetupState(GameStateResult.Success(EmptyState));
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
