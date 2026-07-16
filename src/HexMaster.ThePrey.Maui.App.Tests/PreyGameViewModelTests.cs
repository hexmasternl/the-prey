using System.Diagnostics;
using HexMaster.ThePrey.Maui.App.Services.Api;
using HexMaster.ThePrey.Maui.App.Services.Authentication;
using HexMaster.ThePrey.Maui.App.Services.Localization;
using HexMaster.ThePrey.Maui.App.Services.Location;
using HexMaster.ThePrey.Maui.App.Services.Navigation;
using HexMaster.ThePrey.Maui.App.Services.Session;
using HexMaster.ThePrey.Maui.App.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace HexMaster.ThePrey.Maui.App.Tests;

public class PreyGameViewModelTests
{
    private readonly Mock<IGameApiClient> _api = new();
    private readonly FakeGameStreamClient _stream = new();
    private readonly Mock<ILivePositionReader> _position = new();
    private readonly Mock<IHeadingReader> _heading = new();
    private readonly Mock<IGameplayNavigator> _nav = new();
    private readonly Mock<IAccessTokenProvider> _tokens = new();
    private readonly Mock<ICurrentUserProvider> _currentUser = new();
    private readonly Mock<ILocalizationService> _localization = new();
    private readonly FakeTimeProvider _time = new();
    private readonly Guid _gameId = Guid.NewGuid();
    private readonly Guid _hunterId = Guid.NewGuid();
    private readonly Guid _selfId = Guid.NewGuid();

    public PreyGameViewModelTests()
    {
        _localization.Setup(l => l[It.IsAny<string>()]).Returns((string k) => k);
        _tokens.Setup(t => t.GetAccessTokenAsync(It.IsAny<CancellationToken>())).ReturnsAsync("token");
        _currentUser.Setup(c => c.GetUserIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(_selfId);
    }

    private PreyGameViewModel CreateSut() => new(
        _api.Object, _stream, _position.Object, _heading.Object, _nav.Object,
        _tokens.Object, _currentUser.Object, _localization.Object, _time, NullLogger<PreyGameViewModel>.Instance);

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

    // ---- 7.2 load ----

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
    public async Task LoadAsync_NotFound_ShowsError()
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

    // ---- 7.3 phase ----

    [Fact]
    public async Task Phase_Ready_IsWaiting()
    {
        SetupActive();
        SetupGame("Ready");
        var sut = CreateSut();

        await sut.LoadAsync();

        Assert.Equal(GamePhase.Waiting, sut.Phase);
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
    }

    [Fact]
    public async Task Phase_InProgress_PastMayMove_IsLive()
    {
        SetupActive();
        SetupGame("InProgress");
        SetupStatus(GetGameStatusResult.Success(Status(mayMove: _time.GetUtcNow().AddMinutes(-1))));
        var sut = CreateSut();

        await sut.LoadAsync();

        Assert.Equal(GamePhase.Live, sut.Phase);
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
        SetupStatus(GetGameStatusResult.Forbidden);
        var sut = CreateSut();

        await sut.LoadAsync();

        Assert.False(sut.HasError);
    }

    // ---- 7.4 blip projection ----

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
        SetupActive();
        SetupGame("InProgress");
        SetupStatus(GetGameStatusResult.Success(Status(mayMove: null,
            new GameParticipantStatusDetails(_hunterId, new GpsCoordinate(0, 0), "Active"),
            new GameParticipantStatusDetails(otherPrey, new GpsCoordinate(1, 1), "Active"),
            new GameParticipantStatusDetails(_selfId, new GpsCoordinate(2, 2), "Active"))));
        var sut = CreateSut();

        await sut.LoadAsync();

        Assert.Equal(2, sut.Blips.Count);
        Assert.Equal(MapBlipRole.Hunter, sut.Blips.Single(b => b.Id == _hunterId).Role);
        Assert.Equal(MapBlipRole.Prey, sut.Blips.Single(b => b.Id == otherPrey).Role);
        Assert.DoesNotContain(sut.Blips, b => b.Id == _selfId);
    }

    // ---- 7.5 live updates ----

    [Fact]
    public async Task Live_ParticipantLocated_ForHunter_AddsRedDot()
    {
        SetupActive();
        SetupGame("InProgress");
        SetupStatus(GetGameStatusResult.Success(Status(mayMove: null)));
        using var sut = CreateSut();
        await sut.ActivateAsync();

        _stream.Emit(new GameStreamEvent.ParticipantLocated(_hunterId, 1, 2, "Active"));

        await WaitFor(() => sut.Blips.Count == 1, "the hunter dot is added");
        Assert.Equal(MapBlipRole.Hunter, sut.Blips[0].Role);
        sut.Deactivate();
    }

    [Fact]
    public async Task Live_ParticipantStatusChanged_RecolorsOtherPreyToCaught()
    {
        var otherPrey = Guid.NewGuid();
        SetupActive();
        SetupGame("InProgress");
        SetupStatus(GetGameStatusResult.Success(Status(mayMove: null,
            new GameParticipantStatusDetails(otherPrey, new GpsCoordinate(1, 1), "Active"))));
        using var sut = CreateSut();
        await sut.ActivateAsync();
        Assert.Equal(MapBlipRole.Prey, sut.Blips.Single().Role);

        _stream.Emit(new GameStreamEvent.ParticipantStatusChanged(otherPrey, "Tagged"));

        await WaitFor(() => sut.Blips.Single().Role == MapBlipRole.Caught, "the other prey greys out");
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

    // ---- 7.6 spectator ----

    [Fact]
    public async Task Spectator_SelfTagged_SetsSpectating_KeepsConnectionsAlive_NoHandoff()
    {
        SetupActive();
        SetupGame("InProgress");
        SetupStatus(GetGameStatusResult.Success(Status(mayMove: null)));
        using var sut = CreateSut();
        await sut.ActivateAsync();

        _stream.Emit(new GameStreamEvent.ParticipantStatusChanged(_selfId, "Tagged"));

        await WaitFor(() => sut.Spectating, "spectating is set");
        Assert.NotEqual(GamePhase.Ended, sut.Phase);
        Assert.True(_stream.IsSubscribed, "the channel stays connected");
        _nav.Verify(n => n.GoToOutcomeAsync(), Times.Never);
        sut.Deactivate();
    }

    [Fact]
    public async Task Spectator_ThenGameEnded_HandsOff()
    {
        SetupActive();
        SetupGame("InProgress");
        SetupStatus(GetGameStatusResult.Success(Status(mayMove: null)));
        using var sut = CreateSut();
        await sut.ActivateAsync();

        _stream.Emit(new GameStreamEvent.ParticipantStatusChanged(_selfId, "Tagged"));
        await WaitFor(() => sut.Spectating, "spectating is set");
        _stream.Emit(new GameStreamEvent.GameEnded("HunterWins", 0));

        await WaitFor(() => sut.Phase == GamePhase.Ended, "the game ends");
        await Task.Delay(50);
        _nav.Verify(n => n.GoToOutcomeAsync(), Times.Once);
        sut.Deactivate();
    }

    // ---- 7.7 head-start ----

    [Fact]
    public async Task HeadStart_CountdownDerivesFromMayMove_AndShowsPreyPenaltyWarning()
    {
        SetupActive();
        SetupGame("InProgress");
        SetupStatus(GetGameStatusResult.Success(Status(mayMove: _time.GetUtcNow().AddSeconds(90))));
        using var sut = CreateSut();

        await sut.ActivateAsync();

        Assert.Equal(GamePhase.HeadStart, sut.Phase);
        Assert.Equal("01:30", sut.HeadStartCountdownText);
        Assert.True(sut.ShowPenaltyWarning); // prey overlay shows the (prey-framed) warning during head-start

        _time.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal("01:29", sut.HeadStartCountdownText);

        sut.Deactivate();
    }

    [Fact]
    public async Task HeadStart_ReAnchorsFromNewSnapshot()
    {
        SetupActive();
        SetupGame("InProgress");
        SetupStatus(GetGameStatusResult.Success(Status(mayMove: _time.GetUtcNow().AddSeconds(90))));
        var sut = CreateSut();

        await sut.LoadAsync();
        Assert.Equal("01:30", sut.HeadStartCountdownText);

        SetupStatus(GetGameStatusResult.Success(Status(mayMove: _time.GetUtcNow().AddSeconds(300))));
        await sut.LoadAsync();

        Assert.Equal("05:00", sut.HeadStartCountdownText);
        Assert.Equal(GamePhase.HeadStart, sut.Phase);
    }
}
