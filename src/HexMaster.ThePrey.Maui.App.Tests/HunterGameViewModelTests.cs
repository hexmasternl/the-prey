using System.Diagnostics;
using HexMaster.ThePrey.Maui.App.Services.Api;
using HexMaster.ThePrey.Maui.App.Services.Authentication;
using HexMaster.ThePrey.Maui.App.Services.Localization;
using HexMaster.ThePrey.Maui.App.Services.Location;
using HexMaster.ThePrey.Maui.App.Services.Navigation;
using HexMaster.ThePrey.Maui.App.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace HexMaster.ThePrey.Maui.App.Tests;

public class HunterGameViewModelTests
{
    private readonly Mock<IGameApiClient> _api = new();
    private readonly FakeGameStreamClient _stream = new();
    private readonly Mock<ILivePositionReader> _position = new();
    private readonly Mock<IHeadingReader> _heading = new();
    private readonly Mock<IGameplayNavigator> _nav = new();
    private readonly Mock<IAccessTokenProvider> _tokens = new();
    private readonly Mock<ILocalizationService> _localization = new();
    private readonly FakeTimeProvider _time = new();
    private readonly Guid _gameId = Guid.NewGuid();
    private readonly Guid _hunterId = Guid.NewGuid();

    public HunterGameViewModelTests()
    {
        _localization.Setup(l => l[It.IsAny<string>()]).Returns((string k) => k);
        _tokens.Setup(t => t.GetAccessTokenAsync(It.IsAny<CancellationToken>())).ReturnsAsync("token");
    }

    private HunterGameViewModel CreateSut() => new(
        _api.Object, _stream, _position.Object, _heading.Object, _nav.Object,
        _tokens.Object, _localization.Object, _time, NullLogger<HunterGameViewModel>.Instance);

    private void SetupActive(ActiveGameResult? result = null) =>
        _api.Setup(a => a.GetActiveGameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result ?? ActiveGameResult.Active(new GameStatus { GameId = _gameId }));

    private void SetupGame(string status, GetGameOutcome outcome = GetGameOutcome.Success)
    {
        var result = outcome == GetGameOutcome.Success
            ? GetGameResult.Success(new GameDetails(
                _gameId, "1234", status, new GameConfigurationDetails(30, 5, 10, 120, 60),
                Array.Empty<GameParticipantDetails>(), _hunterId, Guid.NewGuid(), false, false))
            : outcome switch
            {
                GetGameOutcome.NotFound => GetGameResult.NotFound,
                GetGameOutcome.Unauthorized => GetGameResult.Unauthorized,
                _ => GetGameResult.Error
            };
        _api.Setup(a => a.GetGameAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);
    }

    private void SetupStatus(GetGameStatusResult result) =>
        _api.Setup(a => a.GetGameStatusDetailsAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

    private GameStatusDetails Status(DateTimeOffset? mayMove = null, params GameParticipantStatusDetails[] participants) =>
        new(Array.Empty<GpsCoordinate>(), participants, _hunterId, GameDurationLeft: 600, mayMove, IsEndgame: false, PreysLeft: 1);

    private static async Task WaitFor(Func<bool> condition, string because)
    {
        var sw = Stopwatch.StartNew();
        while (!condition() && sw.Elapsed < TimeSpan.FromSeconds(5))
            await Task.Delay(10);
        Assert.True(condition(), because);
    }

    // ---- 9.2 load ----

    [Fact]
    public async Task LoadAsync_ShouldResolveActiveGame_AndSeedStatus()
    {
        SetupActive();
        SetupGame("InProgress");
        SetupStatus(GetGameStatusResult.Success(Status(mayMove: null)));
        var sut = CreateSut();

        await sut.LoadAsync();

        Assert.Equal(_gameId, sut.GameId);
        Assert.False(sut.HasError);
    }

    [Fact]
    public async Task LoadAsync_NoActiveGame_ShowsError()
    {
        SetupActive(ActiveGameResult.None);
        var sut = CreateSut();

        await sut.LoadAsync();

        Assert.True(sut.HasError);
    }

    [Fact]
    public async Task LoadAsync_GameNotFound_ShowsError()
    {
        SetupActive();
        SetupGame("InProgress", GetGameOutcome.NotFound);
        var sut = CreateSut();

        await sut.LoadAsync();

        Assert.True(sut.HasError);
    }

    [Fact]
    public async Task LoadAsync_Unauthorized_InvalidatesTokenAndErrors()
    {
        SetupActive(ActiveGameResult.Unauthorized);
        var sut = CreateSut();

        await sut.LoadAsync();

        _tokens.Verify(t => t.Invalidate(), Times.Once);
        Assert.True(sut.HasError);
    }

    // ---- 9.4 phase ----

    [Fact]
    public async Task Phase_Ready_IsWaiting()
    {
        SetupActive();
        SetupGame("Ready");
        var sut = CreateSut();

        await sut.LoadAsync();

        Assert.Equal(GamePhase.Waiting, sut.Phase);
        Assert.True(sut.ShowWaitingOverlay);
    }

    [Fact]
    public async Task Phase_InProgress_FutureMayMove_IsHeadStart()
    {
        SetupActive();
        SetupGame("InProgress");
        SetupStatus(GetGameStatusResult.Success(Status(mayMove: _time.GetUtcNow().AddMinutes(2))));
        var sut = CreateSut();

        await sut.LoadAsync();

        Assert.Equal(GamePhase.HeadStart, sut.Phase);
        Assert.True(sut.ShowHeadStartOverlay);
        Assert.True(sut.ShowPenaltyWarning);
    }

    [Fact]
    public async Task Phase_InProgress_PastOrNullMayMove_IsLive()
    {
        SetupActive();
        SetupGame("InProgress");
        SetupStatus(GetGameStatusResult.Success(Status(mayMove: _time.GetUtcNow().AddMinutes(-1))));
        var sut = CreateSut();

        await sut.LoadAsync();

        Assert.Equal(GamePhase.Live, sut.Phase);
        Assert.False(sut.ShowPenaltyWarning);
    }

    [Fact]
    public async Task Phase_Completed_IsEnded_AndHandsOffOnce()
    {
        SetupActive();
        SetupGame("Completed");
        var sut = CreateSut();

        await sut.LoadAsync();

        Assert.Equal(GamePhase.Ended, sut.Phase);
        _nav.Verify(n => n.GoToOutcomeAsync(), Times.Once);
    }

    [Fact]
    public async Task Phase_StatusForbiddenOrConflict_TreatedAsNotLiveYet_NoError()
    {
        SetupActive();
        SetupGame("InProgress");
        SetupStatus(GetGameStatusResult.Conflict);
        var sut = CreateSut();

        await sut.LoadAsync();

        Assert.False(sut.HasError);
    }

    // ---- 9.5 head-start countdown ----

    [Fact]
    public async Task Countdown_DerivesFromMayMove_ReachesZero_AdvancesToLive()
    {
        SetupActive();
        SetupGame("InProgress");
        SetupStatus(GetGameStatusResult.Success(Status(mayMove: _time.GetUtcNow().AddSeconds(2))));
        using var sut = CreateSut();

        await sut.ActivateAsync();
        Assert.Equal(GamePhase.HeadStart, sut.Phase);
        Assert.Equal("00:02", sut.HeadStartCountdownText);

        _time.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal("00:01", sut.HeadStartCountdownText);

        _time.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal("00:00", sut.HeadStartCountdownText);
        Assert.Equal(GamePhase.Live, sut.Phase);

        sut.Deactivate();
    }

    [Fact]
    public async Task Countdown_ReAnchorsFromNewSnapshot()
    {
        SetupActive();
        SetupGame("InProgress");
        SetupStatus(GetGameStatusResult.Success(Status(mayMove: _time.GetUtcNow().AddSeconds(90))));
        var sut = CreateSut();

        await sut.LoadAsync();
        Assert.Equal("01:30", sut.HeadStartCountdownText);

        // A fresh snapshot with a later may-move re-anchors the countdown (clock unchanged).
        SetupStatus(GetGameStatusResult.Success(Status(mayMove: _time.GetUtcNow().AddSeconds(300))));
        await sut.LoadAsync();

        Assert.Equal("05:00", sut.HeadStartCountdownText);
        Assert.Equal(GamePhase.HeadStart, sut.Phase);
    }

    // ---- 9.6 map projection ----

    [Fact]
    public void Projection_PreyWithLocationActive_IsPreyDot()
    {
        var blip = GameMapProjection.ProjectForHunter(Guid.NewGuid(), _hunterId, new GpsCoordinate(1, 2), "Active");
        Assert.NotNull(blip);
        Assert.Equal(MapBlipRole.Prey, blip!.Role);
    }

    [Fact]
    public void Projection_TaggedOrOut_IsCaughtDot()
    {
        Assert.Equal(MapBlipRole.Caught, GameMapProjection.ProjectForHunter(Guid.NewGuid(), _hunterId, new GpsCoordinate(1, 2), "Tagged")!.Role);
        Assert.Equal(MapBlipRole.Caught, GameMapProjection.ProjectForHunter(Guid.NewGuid(), _hunterId, new GpsCoordinate(1, 2), "Out")!.Role);
    }

    [Fact]
    public void Projection_NoLocation_IsNoDot() =>
        Assert.Null(GameMapProjection.ProjectForHunter(Guid.NewGuid(), _hunterId, null, "Active"));

    [Fact]
    public void Projection_HuntersOwnRow_IsNeverADot() =>
        Assert.Null(GameMapProjection.ProjectForHunter(_hunterId, _hunterId, new GpsCoordinate(1, 2), "Active"));

    [Fact]
    public async Task Seed_BuildsPreyDots_ExcludingHunter()
    {
        var prey = Guid.NewGuid();
        SetupActive();
        SetupGame("InProgress");
        SetupStatus(GetGameStatusResult.Success(Status(mayMove: null,
            new GameParticipantStatusDetails(_hunterId, new GpsCoordinate(0, 0), "Active"),
            new GameParticipantStatusDetails(prey, new GpsCoordinate(1, 1), "Active"))));
        var sut = CreateSut();

        await sut.LoadAsync();

        Assert.Single(sut.Blips);
        Assert.Equal(prey, sut.Blips[0].Id);
    }

    // ---- 9.7 live updates ----

    [Fact]
    public async Task Live_ParticipantLocated_AddsPreyDot()
    {
        var prey = Guid.NewGuid();
        SetupActive();
        SetupGame("InProgress");
        SetupStatus(GetGameStatusResult.Success(Status(mayMove: null)));
        using var sut = CreateSut();
        await sut.ActivateAsync();

        _stream.Emit(new GameStreamEvent.ParticipantLocated(prey, 1, 2, "Active"));

        await WaitFor(() => sut.Blips.Count == 1, "the prey dot is added");
        Assert.Equal(MapBlipRole.Prey, sut.Blips[0].Role);
        sut.Deactivate();
    }

    [Fact]
    public async Task Live_ParticipantStatusChanged_RecolorsDotToCaught()
    {
        var prey = Guid.NewGuid();
        SetupActive();
        SetupGame("InProgress");
        SetupStatus(GetGameStatusResult.Success(Status(mayMove: null,
            new GameParticipantStatusDetails(prey, new GpsCoordinate(1, 1), "Active"))));
        using var sut = CreateSut();
        await sut.ActivateAsync();
        Assert.Equal(MapBlipRole.Prey, sut.Blips.Single().Role);

        _stream.Emit(new GameStreamEvent.ParticipantStatusChanged(prey, "Tagged"));

        await WaitFor(() => sut.Blips.Single().Role == MapBlipRole.Caught, "the dot greys out");
        sut.Deactivate();
    }

    [Fact]
    public async Task Live_GameEnded_HandsOffExactlyOnce()
    {
        SetupActive();
        SetupGame("InProgress");
        SetupStatus(GetGameStatusResult.Success(Status(mayMove: null)));
        using var sut = CreateSut();
        await sut.ActivateAsync();

        _stream.Emit(new GameStreamEvent.GameEnded("HunterWins", 0));
        _stream.Emit(new GameStreamEvent.GameEnded("HunterWins", 0));

        await WaitFor(() => sut.Phase == GamePhase.Ended, "the game ends");
        await Task.Delay(50);
        _nav.Verify(n => n.GoToOutcomeAsync(), Times.Once);
        sut.Deactivate();
    }

    [Fact]
    public async Task Live_PositionAndHeading_UpdateSelfMarker()
    {
        SetupActive();
        SetupGame("InProgress");
        SetupStatus(GetGameStatusResult.Success(Status(mayMove: null)));
        using var sut = CreateSut();
        await sut.ActivateAsync();

        _position.Raise(p => p.PositionChanged += null, new GpsFix(52.1, 4.2));
        _heading.Raise(h => h.HeadingChanged += null, 90d);

        Assert.Equal(52.1, sut.SelfPosition!.Latitude, 5);
        Assert.Equal(90d, sut.Heading);
        sut.Deactivate();
    }

    [Fact]
    public async Task Deactivate_CancelsSubscription_AndStopsReaders()
    {
        SetupActive();
        SetupGame("InProgress");
        SetupStatus(GetGameStatusResult.Success(Status(mayMove: null)));
        var sut = CreateSut();
        await sut.ActivateAsync();
        await WaitFor(() => _stream.IsSubscribed, "the stream is subscribed");

        sut.Deactivate();

        await WaitFor(() => _stream.Completed, "the subscription ends");
        _position.Verify(p => p.Stop(), Times.Once);
        _heading.Verify(h => h.Stop(), Times.Once);
    }
}
