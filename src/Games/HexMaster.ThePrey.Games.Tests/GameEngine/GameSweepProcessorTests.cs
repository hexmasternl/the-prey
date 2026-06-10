using HexMaster.ThePrey.Games.DomainModels;
using HexMaster.ThePrey.Games.GameEngine;
using HexMaster.ThePrey.Games.Observability;
using HexMaster.ThePrey.Games.Tests.Factories;
using HexMaster.ThePrey.IntegrationEvents;
using HexMaster.ThePrey.IntegrationEvents.Events;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace HexMaster.ThePrey.Games.Tests.GameEngine;

public sealed class GameSweepProcessorTests
{
    private static readonly DateTimeOffset Start = new(2026, 6, 3, 12, 0, 0, TimeSpan.Zero);

    // 2x2 square around the origin (lat/lng). Inside: (0,0). Outside: (5,5).
    private static readonly IReadOnlyList<GpsCoordinate> Square =
    [
        GpsCoordinate.Create(1, 1),
        GpsCoordinate.Create(1, -1),
        GpsCoordinate.Create(-1, -1),
        GpsCoordinate.Create(-1, 1),
    ];

    private readonly Mock<IGameRepository> _games = new();
    private readonly Mock<IPlayfieldBoundaryProvider> _boundary = new();
    private readonly Mock<IIntegrationEventPublisher> _publisher = new();
    private readonly Mock<IGameMetrics> _metrics = new();
    private readonly GameSweepProcessor _sut;

    public GameSweepProcessorTests()
    {
        _boundary.Setup(b => b.GetPolygonAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Square);
        _publisher.Setup(p => p.PublishAsync(It.IsAny<IIntegrationEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _sut = new GameSweepProcessor(
            _games.Object,
            _boundary.Object,
            new RayCastingBoundaryChecker(),
            _publisher.Object,
            _metrics.Object,
            NullLogger<GameSweepProcessor>.Instance);
    }

    private void SetupGame(Game game) =>
        _games.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);

    [Fact]
    public async Task ProcessAsync_ShouldReturnNone_WhenGameIsNotInProgress()
    {
        var game = GameFaker.LobbyGameWithPlayers(2, out _); // Lobby state
        SetupGame(game);

        var result = await _sut.ProcessAsync(game.Id, Start, CancellationToken.None);

        Assert.Equal(GameTickResult.None, result);
        _games.Verify(r => r.UpdateAsync(It.IsAny<Game>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessAsync_ShouldBroadcastUncheckedReadingsAndMarkThemChecked()
    {
        var game = GameFaker.StartedGame(out _, out var preyIds, Start, playerCount: 3);
        game.RecordLocation(preyIds[0], GpsCoordinate.Create(0, 0), Start.AddSeconds(5));
        SetupGame(game);

        var result = await _sut.ProcessAsync(game.Id, Start.AddMinutes(1), CancellationToken.None);

        Assert.Equal(1, result.Broadcasts);
        var prey = game.Participants.Single(p => p.UserId == preyIds[0]);
        Assert.NotNull(prey.Location);
        Assert.All(prey.Locations, l => Assert.True(l.Checked));
        _publisher.Verify(p => p.PublishAsync(
            It.Is<PlayerLocationUpdatedIntegrationEvent>(e => e.UserId == preyIds[0]),
            It.IsAny<CancellationToken>()), Times.Once);
        _games.Verify(r => r.UpdateAsync(game, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_ShouldNotBroadcastAlreadyCheckedReadings_OnSecondTick()
    {
        var game = GameFaker.StartedGame(out _, out var preyIds, Start, playerCount: 3);
        game.RecordLocation(preyIds[0], GpsCoordinate.Create(0, 0), Start.AddSeconds(5));
        SetupGame(game);

        await _sut.ProcessAsync(game.Id, Start.AddMinutes(1), CancellationToken.None);
        var second = await _sut.ProcessAsync(game.Id, Start.AddMinutes(2), CancellationToken.None);

        Assert.Equal(0, second.Broadcasts);
    }

    [Fact]
    public async Task ProcessAsync_ShouldPenalizePrey_WhenOutsideBoundaryAndPenaltiesEnabled()
    {
        var config = GameFaker.ValidConfiguration(enablePreyBoundaryPenalties: true);
        var game = GameFaker.StartedGame(out _, out var preyIds, Start, playerCount: 3, configuration: config);
        game.RecordLocation(preyIds[0], GpsCoordinate.Create(5, 5), Start.AddSeconds(5)); // outside the square
        SetupGame(game);

        var result = await _sut.ProcessAsync(game.Id, Start.AddMinutes(1), CancellationToken.None);

        Assert.Equal(1, result.Penalties);
        Assert.True(game.Participants.Single(p => p.UserId == preyIds[0]).HasActivePenalty(Start.AddMinutes(1)));
        _publisher.Verify(p => p.PublishAsync(
            It.Is<PlayerPenalizedIntegrationEvent>(e => e.UserId == preyIds[0]),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_ShouldNotPenalize_WhenBoundaryPenaltiesDisabled()
    {
        var game = GameFaker.StartedGame(out _, out var preyIds, Start, playerCount: 3); // penalties off by default
        game.RecordLocation(preyIds[0], GpsCoordinate.Create(5, 5), Start.AddSeconds(5));
        SetupGame(game);

        var result = await _sut.ProcessAsync(game.Id, Start.AddMinutes(1), CancellationToken.None);

        Assert.Equal(0, result.Penalties);
    }

    [Fact]
    public async Task ProcessAsync_ShouldNotStackPenalty_WhenPreyAlreadyPenalized()
    {
        var config = GameFaker.ValidConfiguration(enablePreyBoundaryPenalties: true);
        var game = GameFaker.StartedGame(out _, out var preyIds, Start, playerCount: 3, configuration: config);
        game.RecordLocation(preyIds[0], GpsCoordinate.Create(5, 5), Start.AddSeconds(5));
        game.ApplyPenalty(preyIds[0], Start.AddMinutes(5)); // already penalised
        SetupGame(game);

        var result = await _sut.ProcessAsync(game.Id, Start.AddMinutes(1), CancellationToken.None);

        Assert.Equal(0, result.Penalties);
        _publisher.Verify(p => p.PublishAsync(It.IsAny<PlayerPenalizedIntegrationEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessAsync_ShouldCompleteGameAndEmitGameEnded_WhenScheduledEndPassed()
    {
        var game = GameFaker.StartedGame(out _, out _, Start, playerCount: 3); // 60-minute game
        SetupGame(game);

        var result = await _sut.ProcessAsync(game.Id, Start.AddMinutes(61), CancellationToken.None);

        Assert.True(result.Completed);
        Assert.Equal(GameStatus.Completed, game.Status);
        _publisher.Verify(p => p.PublishAsync(It.IsAny<GameEndedIntegrationEvent>(), It.IsAny<CancellationToken>()), Times.Once);
        _metrics.Verify(m => m.RecordGameCompleted(It.IsAny<string>()), Times.Once);
        _games.Verify(r => r.UpdateAsync(game, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_ShouldNotPersist_WhenNothingChanged()
    {
        var game = GameFaker.StartedGame(out _, out _, Start, playerCount: 3); // no readings, not ended
        SetupGame(game);

        var result = await _sut.ProcessAsync(game.Id, Start.AddMinutes(1), CancellationToken.None);

        Assert.Equal(GameTickResult.None with { }, result);
        _games.Verify(r => r.UpdateAsync(It.IsAny<Game>(), It.IsAny<CancellationToken>()), Times.Never);
        _publisher.Verify(p => p.PublishAsync(It.IsAny<IIntegrationEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
