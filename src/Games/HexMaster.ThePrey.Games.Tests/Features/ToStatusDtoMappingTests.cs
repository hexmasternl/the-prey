using HexMaster.ThePrey.Games.Abstractions.DataTransferObjects;
using HexMaster.ThePrey.Games.DomainModels;
using HexMaster.ThePrey.Games.Tests.Factories;
using Moq;

namespace HexMaster.ThePrey.Games.Tests.Features;

/// <summary>Tests for the ToStatusDto mapping path exercised via the GetGameStatusQueryHandler.</summary>
public sealed class ToStatusDtoMappingTests
{
    [Fact]
    public async Task Handle_ShouldReturnIsEndgame_True_WhenInFinalStage()
    {
        // A game with 60 min duration, 10 min final stage: start 55 min ago puts us in final stage.
        var config = GameFaker.ValidConfiguration(gameDuration: 60, finalStageDuration: 10);
        var startedAt = DateTimeOffset.UtcNow.AddMinutes(-55);
        var game = GameFaker.StartedGame(out var hunterId, out _, startedAt, configuration: config);

        var repoMock = new Mock<IGameRepository>();
        var playfieldsMock = new Mock<IPlayfieldInfoProvider>();

        repoMock.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);
        playfieldsMock.Setup(p => p.GetAsync(game.PlayfieldId, It.IsAny<CancellationToken>())).ReturnsAsync((PlayfieldInfo?)null);

        var handler = new HexMaster.ThePrey.Games.Features.GetGameStatus.GetGameStatusQueryHandler(repoMock.Object, playfieldsMock.Object);
        var result = await handler.Handle(new HexMaster.ThePrey.Games.Features.GetGameStatus.GetGameStatusQuery(game.Id, hunterId), CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(result!.IsEndgame);
    }

    [Fact]
    public async Task Handle_ShouldReturnIsEndgame_False_WhenNotInFinalStage()
    {
        var config = GameFaker.ValidConfiguration(gameDuration: 60, finalStageDuration: 10);
        var startedAt = DateTimeOffset.UtcNow.AddMinutes(-5); // 5 minutes in, not in final stage
        var game = GameFaker.StartedGame(out var hunterId, out _, startedAt, configuration: config);

        var repoMock = new Mock<IGameRepository>();
        var playfieldsMock = new Mock<IPlayfieldInfoProvider>();

        repoMock.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);
        playfieldsMock.Setup(p => p.GetAsync(game.PlayfieldId, It.IsAny<CancellationToken>())).ReturnsAsync((PlayfieldInfo?)null);

        var handler = new HexMaster.ThePrey.Games.Features.GetGameStatus.GetGameStatusQueryHandler(repoMock.Object, playfieldsMock.Object);
        var result = await handler.Handle(new HexMaster.ThePrey.Games.Features.GetGameStatus.GetGameStatusQuery(game.Id, hunterId), CancellationToken.None);

        Assert.NotNull(result);
        Assert.False(result!.IsEndgame);
    }

    [Fact]
    public async Task Handle_ShouldReturnHunterMayMoveAt_AsStartPlusHunterDelayTime()
    {
        var config = GameFaker.ValidConfiguration(hunterDelayTime: 5);
        var startedAt = DateTimeOffset.UtcNow.AddMinutes(-2);
        var game = GameFaker.StartedGame(out var hunterId, out _, startedAt, configuration: config);

        var repoMock = new Mock<IGameRepository>();
        var playfieldsMock = new Mock<IPlayfieldInfoProvider>();

        repoMock.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);
        playfieldsMock.Setup(p => p.GetAsync(game.PlayfieldId, It.IsAny<CancellationToken>())).ReturnsAsync((PlayfieldInfo?)null);

        var handler = new HexMaster.ThePrey.Games.Features.GetGameStatus.GetGameStatusQueryHandler(repoMock.Object, playfieldsMock.Object);
        var result = await handler.Handle(new HexMaster.ThePrey.Games.Features.GetGameStatus.GetGameStatusQuery(game.Id, hunterId), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(startedAt.AddMinutes(5), result!.HunterMayMoveAt);
    }

    [Fact]
    public async Task Handle_ShouldReturnParticipantCallsign_FromLobbyDisplayName()
    {
        var game = GameFaker.LobbyGame();
        var playerId = Guid.NewGuid();
        var expectedCallsign = "TestCallsign";
        game.JoinLobby(GameFaker.Player(playerId, expectedCallsign));
        var secondPlayer = Guid.NewGuid();
        game.JoinLobby(GameFaker.Player(secondPlayer));
        game.SetReady(playerId);
        game.SetReady(secondPlayer);
        game.Start(playerId, DateTimeOffset.UtcNow.AddMinutes(-5));

        var repoMock = new Mock<IGameRepository>();
        var playfieldsMock = new Mock<IPlayfieldInfoProvider>();

        repoMock.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);
        playfieldsMock.Setup(p => p.GetAsync(game.PlayfieldId, It.IsAny<CancellationToken>())).ReturnsAsync((PlayfieldInfo?)null);

        var handler = new HexMaster.ThePrey.Games.Features.GetGameStatus.GetGameStatusQueryHandler(repoMock.Object, playfieldsMock.Object);
        var result = await handler.Handle(new HexMaster.ThePrey.Games.Features.GetGameStatus.GetGameStatusQuery(game.Id, playerId), CancellationToken.None);

        Assert.NotNull(result);
        var hunterStatus = result!.Participants.Single(p => p.UserId == playerId);
        Assert.Equal(expectedCallsign, hunterStatus.Callsign);
    }

    [Fact]
    public async Task Handle_ShouldReturnNullLastKnownLocation_WhenParticipantHasNoLocation()
    {
        var game = GameFaker.StartedGame(out var hunterId, out _, DateTimeOffset.UtcNow.AddMinutes(-5));

        var repoMock = new Mock<IGameRepository>();
        var playfieldsMock = new Mock<IPlayfieldInfoProvider>();

        repoMock.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);
        playfieldsMock.Setup(p => p.GetAsync(game.PlayfieldId, It.IsAny<CancellationToken>())).ReturnsAsync((PlayfieldInfo?)null);

        var handler = new HexMaster.ThePrey.Games.Features.GetGameStatus.GetGameStatusQueryHandler(repoMock.Object, playfieldsMock.Object);
        var result = await handler.Handle(new HexMaster.ThePrey.Games.Features.GetGameStatus.GetGameStatusQuery(game.Id, hunterId), CancellationToken.None);

        Assert.NotNull(result);
        var hunterStatus = result!.Participants.Single(p => p.UserId == hunterId);
        Assert.Null(hunterStatus.LastKnownLocation);
    }

    [Fact]
    public async Task Handle_ShouldSetHunterStateToActive_Always()
    {
        var game = GameFaker.StartedGame(out var hunterId, out _, DateTimeOffset.UtcNow.AddMinutes(-5));

        var repoMock = new Mock<IGameRepository>();
        var playfieldsMock = new Mock<IPlayfieldInfoProvider>();
        repoMock.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);
        playfieldsMock.Setup(p => p.GetAsync(game.PlayfieldId, It.IsAny<CancellationToken>())).ReturnsAsync((PlayfieldInfo?)null);

        var handler = new HexMaster.ThePrey.Games.Features.GetGameStatus.GetGameStatusQueryHandler(repoMock.Object, playfieldsMock.Object);
        var result = await handler.Handle(new HexMaster.ThePrey.Games.Features.GetGameStatus.GetGameStatusQuery(game.Id, hunterId), CancellationToken.None);

        Assert.NotNull(result);
        var hunterStatus = result!.Participants.Single(p => p.UserId == hunterId);
        Assert.Equal("Active", hunterStatus.State);
    }

    [Fact]
    public async Task Handle_ShouldSetPreyStateToTagged_WhenPreyIsTagged()
    {
        var startedAt = DateTimeOffset.UtcNow.AddMinutes(-5);
        var game = GameFaker.StartedGame(out var hunterId, out var preyIds, startedAt);
        var preyId = preyIds[0];
        GameFaker.RecordColocated(game, startedAt.AddMinutes(1), hunterId, preyId);
        game.TagParticipant(hunterId, preyId, startedAt.AddMinutes(10));

        var repoMock = new Mock<IGameRepository>();
        var playfieldsMock = new Mock<IPlayfieldInfoProvider>();
        repoMock.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);
        playfieldsMock.Setup(p => p.GetAsync(game.PlayfieldId, It.IsAny<CancellationToken>())).ReturnsAsync((PlayfieldInfo?)null);

        var handler = new HexMaster.ThePrey.Games.Features.GetGameStatus.GetGameStatusQueryHandler(repoMock.Object, playfieldsMock.Object);
        var result = await handler.Handle(new HexMaster.ThePrey.Games.Features.GetGameStatus.GetGameStatusQuery(game.Id, hunterId), CancellationToken.None);

        Assert.NotNull(result);
        var taggedPrey = result!.Participants.Single(p => p.UserId == preyId);
        Assert.Equal("Tagged", taggedPrey.State);
    }

    [Fact]
    public async Task Handle_ShouldCountOnlyActiveAndPassivePreys_InPreysLeft()
    {
        var startedAt = DateTimeOffset.UtcNow.AddMinutes(-5);
        // 3 preys total
        var game = GameFaker.StartedGame(out var hunterId, out var preyIds, startedAt, playerCount: 4);

        // Tag one prey
        GameFaker.RecordColocated(game, startedAt.AddMinutes(1), hunterId, preyIds[0]);
        game.TagParticipant(hunterId, preyIds[0], startedAt.AddMinutes(10));
        // Set one to Out via timeout
        game.RecordLocation(preyIds[1], GpsCoordinate.Create(52.0, 5.0), startedAt);
        game.ApplyTimeoutTransitions(startedAt.AddMinutes(8)); // → Out

        var repoMock = new Mock<IGameRepository>();
        var playfieldsMock = new Mock<IPlayfieldInfoProvider>();
        repoMock.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);
        playfieldsMock.Setup(p => p.GetAsync(game.PlayfieldId, It.IsAny<CancellationToken>())).ReturnsAsync((PlayfieldInfo?)null);

        var handler = new HexMaster.ThePrey.Games.Features.GetGameStatus.GetGameStatusQueryHandler(repoMock.Object, playfieldsMock.Object);
        var result = await handler.Handle(new HexMaster.ThePrey.Games.Features.GetGameStatus.GetGameStatusQuery(game.Id, hunterId), CancellationToken.None);

        Assert.NotNull(result);
        // 3 preys, 1 Tagged, 1 Out → PreysLeft = 1 Active
        Assert.Equal(1, result!.PreysLeft);
    }
}
