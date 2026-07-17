using HexMaster.ThePrey.Maui.App.Services.Api;
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

public class PreyGameViewModelTests
{
    private readonly FakeGameStateService _state = new();
    private readonly Mock<ILivePositionReader> _position = new();
    private readonly Mock<IHeadingReader> _heading = new();
    private readonly Mock<IGameplayNavigator> _nav = new();
    private readonly Mock<ICurrentUserProvider> _currentUser = new();
    private readonly Mock<IGameLocationTracker> _locationTracker = new();
    private readonly Mock<ILocalizationService> _localization = new();
    private readonly FakeTimeProvider _time = new();
    private readonly Guid _gameId = Guid.NewGuid();
    private readonly Guid _hunterId = Guid.NewGuid();
    private readonly Guid _selfId = Guid.NewGuid();

    public PreyGameViewModelTests()
    {
        _localization.Setup(l => l[It.IsAny<string>()]).Returns((string k) => k);
        _currentUser.Setup(c => c.GetUserIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(_selfId);
    }

    private PreyGameViewModel CreateSut() => new(
        _state, _position.Object, _heading.Object, _nav.Object,
        _currentUser.Object, _locationTracker.Object, _localization.Object, _time,
        NullLogger<PreyGameViewModel>.Instance);

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
    public async Task LoadAsync_ShouldStartStore_SeedState_AndResolveSelf()
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
        _state.SeedState = null; // store could not resolve an active game
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
    }

    [Fact]
    public async Task Phase_InProgress_FutureMayMove_IsHeadStart()
    {
        _state.SeedState = State("InProgress", _time.GetUtcNow().AddMinutes(2));
        var sut = CreateSut();

        await sut.LoadAsync();

        Assert.Equal(GamePhase.HeadStart, sut.Phase);
    }

    [Fact]
    public async Task Phase_InProgress_PastMayMove_IsLive()
    {
        _state.SeedState = State("InProgress", _time.GetUtcNow().AddMinutes(-1));
        var sut = CreateSut();

        await sut.LoadAsync();

        Assert.Equal(GamePhase.Live, sut.Phase);
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

    // ---- blip projection (pure) ----

    [Fact]
    public void Projection_Hunter_IsRedDot() =>
        Assert.Equal(MapBlipRole.Hunter, GameMapProjection.ProjectForPrey(_hunterId, _selfId, _hunterId, new GpsCoordinate(1, 2), "Active")!.Role);

    [Fact]
    public void Projection_OtherPreyActive_IsGreenDot() =>
        Assert.Equal(MapBlipRole.Prey, GameMapProjection.ProjectForPrey(Guid.NewGuid(), _selfId, _hunterId, new GpsCoordinate(1, 2), "Active")!.Role);

    [Fact]
    public void Projection_TaggedOrOut_IsGreyDot()
    {
        Assert.Equal(MapBlipRole.Caught, GameMapProjection.ProjectForPrey(Guid.NewGuid(), _selfId, _hunterId, new GpsCoordinate(1, 2), "Tagged")!.Role);
        Assert.Equal(MapBlipRole.Caught, GameMapProjection.ProjectForPrey(Guid.NewGuid(), _selfId, _hunterId, new GpsCoordinate(1, 2), "Out")!.Role);
    }

    [Fact]
    public void Projection_NoLocation_IsNoDot() =>
        Assert.Null(GameMapProjection.ProjectForPrey(Guid.NewGuid(), _selfId, _hunterId, null, "Active"));

    [Fact]
    public void Projection_OwnRow_IsNeverADot() =>
        Assert.Null(GameMapProjection.ProjectForPrey(_selfId, _selfId, _hunterId, new GpsCoordinate(1, 2), "Active"));

    [Fact]
    public async Task Seed_BuildsDots_HunterRedOtherPreyGreen_ExcludingSelf()
    {
        var otherPrey = Guid.NewGuid();
        _state.SeedState = State("InProgress", null,
            P(_hunterId, "Active", new GpsCoordinate(0, 0)),
            P(otherPrey, "Active", new GpsCoordinate(1, 1)),
            P(_selfId, "Active", new GpsCoordinate(2, 2)));
        var sut = CreateSut();

        await sut.LoadAsync();

        Assert.Equal(2, sut.Blips.Count);
        Assert.Equal(MapBlipRole.Hunter, sut.Blips.Single(b => b.Id == _hunterId).Role);
        Assert.Equal(MapBlipRole.Prey, sut.Blips.Single(b => b.Id == otherPrey).Role);
        Assert.DoesNotContain(sut.Blips, b => b.Id == _selfId);
    }

    // ---- live updates ----

    [Fact]
    public async Task Live_HunterLocationSnapshot_AddsRedDot()
    {
        _state.SeedState = State("InProgress");
        using var sut = CreateSut();
        await sut.ActivateAsync();

        _state.Push(State("InProgress", null, P(_hunterId, "Active", new GpsCoordinate(1, 2))));

        Assert.Single(sut.Blips);
        Assert.Equal(MapBlipRole.Hunter, sut.Blips[0].Role);
        sut.Deactivate();
    }

    [Fact]
    public async Task Live_ParticipantStatusChanged_RecolorsOtherPreyToCaught()
    {
        var otherPrey = Guid.NewGuid();
        _state.SeedState = State("InProgress", null, P(otherPrey, "Active", new GpsCoordinate(1, 1)));
        using var sut = CreateSut();
        await sut.ActivateAsync();
        Assert.Equal(MapBlipRole.Prey, sut.Blips.Single().Role);

        _state.Push(State("InProgress", null, P(otherPrey, "Tagged", new GpsCoordinate(1, 1))));

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

    // ---- spectator ----

    [Fact]
    public async Task Spectator_SelfTagged_SetsSpectating_KeepsConnectionsAlive_NoHandoff()
    {
        _state.SeedState = State("InProgress");
        using var sut = CreateSut();
        await sut.ActivateAsync();

        _state.Push(State("InProgress", null, P(_selfId, "Tagged", new GpsCoordinate(2, 2))));

        Assert.True(sut.Spectating);
        Assert.NotEqual(GamePhase.Ended, sut.Phase);
        Assert.False(_state.Stopped); // the channel stays connected
        _nav.Verify(n => n.GoToOutcomeAsync(), Times.Never);
        sut.Deactivate();
    }

    [Fact]
    public async Task Spectator_ThenGameEnded_HandsOff()
    {
        _state.SeedState = State("InProgress");
        using var sut = CreateSut();
        await sut.ActivateAsync();

        _state.Push(State("InProgress", null, P(_selfId, "Tagged", new GpsCoordinate(2, 2))));
        Assert.True(sut.Spectating);
        _state.Push(State("Completed"));

        Assert.Equal(GamePhase.Ended, sut.Phase);
        _nav.Verify(n => n.GoToOutcomeAsync(), Times.Once);
        sut.Deactivate();
    }

    // ---- head-start ----

    [Fact]
    public async Task HeadStart_CountdownDerivesFromMayMove_AndShowsPreyPenaltyWarning()
    {
        _state.SeedState = State("InProgress", _time.GetUtcNow().AddSeconds(90));
        using var sut = CreateSut();

        await sut.ActivateAsync();

        Assert.Equal(GamePhase.HeadStart, sut.Phase);
        Assert.Equal("01:30", sut.HeadStartCountdownText);
        Assert.True(sut.ShowPenaltyWarning);

        _time.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal("01:29", sut.HeadStartCountdownText);

        sut.Deactivate();
    }

    [Fact]
    public async Task HeadStart_ReAnchorsFromNewSnapshot()
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
}
