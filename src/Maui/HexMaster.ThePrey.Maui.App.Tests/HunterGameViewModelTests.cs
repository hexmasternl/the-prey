using HexMaster.ThePrey.Maui.App.Services.Api;
using HexMaster.ThePrey.Maui.App.Services.Localization;
using HexMaster.ThePrey.Maui.App.Services.Location;
using HexMaster.ThePrey.Maui.App.Services.Navigation;
using HexMaster.ThePrey.Maui.App.Services.Realtime;
using HexMaster.ThePrey.Maui.App.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace HexMaster.ThePrey.Maui.App.Tests;

public class HunterGameViewModelTests
{
    private readonly FakeGameStateService _state = new();
    private readonly Mock<ILivePositionReader> _position = new();
    private readonly Mock<IHeadingReader> _heading = new();
    private readonly Mock<IGameplayNavigator> _nav = new();
    private readonly Mock<IGameLocationTracker> _locationTracker = new();
    private readonly Mock<ILocalizationService> _localization = new();
    private readonly FakeTimeProvider _time = new();
    private readonly Guid _gameId = Guid.NewGuid();
    private readonly Guid _hunterId = Guid.NewGuid();

    public HunterGameViewModelTests()
    {
        _localization.Setup(l => l[It.IsAny<string>()]).Returns((string k) => k);
    }

    private HunterGameViewModel CreateSut() => new(
        _state, _position.Object, _heading.Object, _nav.Object,
        _locationTracker.Object, _localization.Object, _time,
        NullLogger<HunterGameViewModel>.Instance);

    private GameLiveState State(
        string status = "InProgress", DateTimeOffset? mayMove = null, params GameLiveParticipant[] participants) =>
        new()
        {
            GameId = _gameId,
            Status = status,
            HunterUserId = _hunterId,
            HunterMayMoveAt = mayMove,
            Participants = participants,
            PlayfieldCoordinates = Array.Empty<GpsCoordinate>(),
        };

    private static GameLiveParticipant P(Guid id, string state = "Active", GpsCoordinate? location = null) =>
        new(id, state, location);

    // ---- load ----

    [Fact]
    public async Task LoadAsync_ShouldStartStore_AndSeedState()
    {
        _state.SeedState = State("InProgress");
        var sut = CreateSut();

        await sut.LoadAsync();

        Assert.True(_state.StartAsyncCalled);
        Assert.Equal(_gameId, sut.GameId);
        Assert.False(sut.HasError);
    }

    [Fact]
    public async Task LoadAsync_NoActiveGame_ShowsError()
    {
        _state.SeedState = null;
        var sut = CreateSut();

        await sut.LoadAsync();

        Assert.True(sut.HasError);
    }

    // ---- phase ----

    [Fact]
    public async Task Phase_Started_IsWaiting()
    {
        // Started is the armed, pre-commit state the gameplay page is entered in — shows the waiting overlay.
        _state.SeedState = State("Started");
        var sut = CreateSut();

        await sut.LoadAsync();

        Assert.Equal(GamePhase.Waiting, sut.Phase);
        Assert.True(sut.ShowWaitingOverlay);
    }

    [Fact]
    public async Task Phase_InProgress_FutureMayMove_IsHeadStart()
    {
        _state.SeedState = State("InProgress", _time.GetUtcNow().AddMinutes(2));
        var sut = CreateSut();

        await sut.LoadAsync();

        Assert.Equal(GamePhase.HeadStart, sut.Phase);
        Assert.True(sut.ShowHeadStartOverlay);
        Assert.True(sut.ShowPenaltyWarning);
    }

    [Fact]
    public async Task Phase_InProgress_PastMayMove_IsLive()
    {
        _state.SeedState = State("InProgress", _time.GetUtcNow().AddMinutes(-1));
        var sut = CreateSut();

        await sut.LoadAsync();

        Assert.Equal(GamePhase.Live, sut.Phase);
        Assert.False(sut.ShowPenaltyWarning);
    }

    [Fact]
    public async Task Phase_Completed_IsEnded_AndHandsOffOnce()
    {
        _state.SeedState = State("Completed");
        var sut = CreateSut();

        await sut.LoadAsync();

        Assert.Equal(GamePhase.Ended, sut.Phase);
        _nav.Verify(n => n.GoToOutcomeAsync(), Times.Once);
    }

    // ---- head-start countdown ----

    [Fact]
    public async Task Countdown_DerivesFromMayMove_ReachesZero_AdvancesToLive()
    {
        _state.SeedState = State("InProgress", _time.GetUtcNow().AddSeconds(2));
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
        _state.SeedState = State("InProgress", _time.GetUtcNow().AddSeconds(90));
        using var sut = CreateSut();

        await sut.ActivateAsync();
        Assert.Equal("01:30", sut.HeadStartCountdownText);

        _state.Push(State("InProgress", _time.GetUtcNow().AddSeconds(300)));

        Assert.Equal("05:00", sut.HeadStartCountdownText);
        Assert.Equal(GamePhase.HeadStart, sut.Phase);
        sut.Deactivate();
    }

    // ---- map projection (pure) ----

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
        _state.SeedState = State("InProgress", null,
            P(_hunterId, "Active", new GpsCoordinate(0, 0)),
            P(prey, "Active", new GpsCoordinate(1, 1)));
        var sut = CreateSut();

        await sut.LoadAsync();

        Assert.Single(sut.Blips);
        Assert.Equal(prey, sut.Blips[0].Id);
    }

    // ---- live updates ----

    [Fact]
    public async Task Live_PreyLocationSnapshot_AddsPreyDot()
    {
        var prey = Guid.NewGuid();
        _state.SeedState = State("InProgress");
        using var sut = CreateSut();
        await sut.ActivateAsync();

        _state.Push(State("InProgress", null, P(prey, "Active", new GpsCoordinate(1, 2))));

        Assert.Single(sut.Blips);
        Assert.Equal(MapBlipRole.Prey, sut.Blips[0].Role);
        sut.Deactivate();
    }

    [Fact]
    public async Task Live_ParticipantStatusChanged_RecolorsDotToCaught()
    {
        var prey = Guid.NewGuid();
        _state.SeedState = State("InProgress", null, P(prey, "Active", new GpsCoordinate(1, 1)));
        using var sut = CreateSut();
        await sut.ActivateAsync();
        Assert.Equal(MapBlipRole.Prey, sut.Blips.Single().Role);

        _state.Push(State("InProgress", null, P(prey, "Tagged", new GpsCoordinate(1, 1))));

        Assert.Equal(MapBlipRole.Caught, sut.Blips.Single().Role);
        sut.Deactivate();
    }

    [Fact]
    public async Task Live_GameEnded_HandsOffExactlyOnce()
    {
        _state.SeedState = State("InProgress");
        using var sut = CreateSut();
        await sut.ActivateAsync();

        _state.Push(State("Completed"));
        _state.Push(State("Completed"));

        Assert.Equal(GamePhase.Ended, sut.Phase);
        _nav.Verify(n => n.GoToOutcomeAsync(), Times.Once);
        sut.Deactivate();
    }

    [Fact]
    public async Task Live_PositionAndHeading_UpdateSelfMarker()
    {
        _state.SeedState = State("InProgress");
        using var sut = CreateSut();
        await sut.ActivateAsync();

        _position.Raise(p => p.PositionChanged += null, new GpsFix(52.1, 4.2));
        _heading.Raise(h => h.HeadingChanged += null, 90d);

        Assert.Equal(52.1, sut.SelfPosition!.Latitude, 5);
        Assert.Equal(90d, sut.Heading);
        sut.Deactivate();
    }

    [Fact]
    public async Task Deactivate_Unsubscribes_StopsStore_AndStopsReaders()
    {
        _state.SeedState = State("InProgress");
        var sut = CreateSut();
        await sut.ActivateAsync();
        Assert.Equal(1, _state.SubscriberCount);

        sut.Deactivate();

        Assert.Equal(0, _state.SubscriberCount);
        Assert.True(_state.Stopped);
        _position.Verify(p => p.Stop(), Times.Once);
        _heading.Verify(h => h.Stop(), Times.Once);
    }
}
