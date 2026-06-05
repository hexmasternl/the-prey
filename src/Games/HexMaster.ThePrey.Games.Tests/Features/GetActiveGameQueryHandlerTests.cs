using HexMaster.ThePrey.Games.Abstractions.DataTransferObjects;
using HexMaster.ThePrey.Games.DomainModels;
using HexMaster.ThePrey.Games.Features.GetActiveGame;
using HexMaster.ThePrey.Games.Tests.Factories;
using Moq;

namespace HexMaster.ThePrey.Games.Tests.Features;

public sealed class GetActiveGameQueryHandlerTests
{
    private readonly Mock<IGameRepository> _repositoryMock = new();
    private readonly Mock<IPlayfieldInfoProvider> _playfieldsMock = new();
    private readonly GetActiveGameQueryHandler _sut;

    public GetActiveGameQueryHandlerTests()
    {
        _sut = new GetActiveGameQueryHandler(_repositoryMock.Object, _playfieldsMock.Object);
    }

    [Fact]
    public async Task Handle_ShouldReturnNull_WhenNoInProgressGame()
    {
        var userId = Guid.NewGuid();
        var lobbyGame = GameFaker.LobbyGame(ownerId: userId);

        _repositoryMock
            .Setup(r => r.ListForUserAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([lobbyGame]);

        var result = await _sut.Handle(new GetActiveGameQuery(userId), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task Handle_ShouldReturnNull_WhenUserHasNoGames()
    {
        var userId = Guid.NewGuid();

        _repositoryMock
            .Setup(r => r.ListForUserAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await _sut.Handle(new GetActiveGameQuery(userId), CancellationToken.None);

        Assert.Null(result);
        _playfieldsMock.Verify(p => p.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldReturnGameStatus_WhenInProgressGameFound()
    {
        var startedAt = DateTimeOffset.UtcNow.AddMinutes(-5);
        var game = GameFaker.StartedGame(out var hunterId, out _, startedAt);

        var playfieldInfo = new PlayfieldInfo("Arena", [new GpsCoordinateDto(0, 0), new GpsCoordinateDto(1, 0), new GpsCoordinateDto(1, 1)]);

        _repositoryMock
            .Setup(r => r.ListForUserAsync(hunterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([game]);

        _playfieldsMock
            .Setup(p => p.GetAsync(game.PlayfieldId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(playfieldInfo);

        var result = await _sut.Handle(new GetActiveGameQuery(hunterId), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(game.Id, result!.GameId);
        Assert.Equal("Arena", result.PlayfieldName);
    }

    [Fact]
    public async Task Handle_ShouldFetchPlayfieldInfo_WhenActiveGameFound()
    {
        var game = GameFaker.StartedGame(out var hunterId, out _, DateTimeOffset.UtcNow.AddMinutes(-5));

        _repositoryMock
            .Setup(r => r.ListForUserAsync(hunterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([game]);

        _playfieldsMock
            .Setup(p => p.GetAsync(game.PlayfieldId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PlayfieldInfo?)null);

        await _sut.Handle(new GetActiveGameQuery(hunterId), CancellationToken.None);

        _playfieldsMock.Verify(p => p.GetAsync(game.PlayfieldId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldPickInProgressGame_WhenMultipleGamesExist()
    {
        var userId = Guid.NewGuid();
        var lobbyGame = GameFaker.LobbyGame(ownerId: userId);
        var startedGame = GameFaker.StartedGame(out var hunterId, out _, DateTimeOffset.UtcNow.AddMinutes(-1));

        _repositoryMock
            .Setup(r => r.ListForUserAsync(hunterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([lobbyGame, startedGame]);

        _playfieldsMock
            .Setup(p => p.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PlayfieldInfo?)null);

        var result = await _sut.Handle(new GetActiveGameQuery(hunterId), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(startedGame.Id, result!.GameId);
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenQueryIsNull()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _sut.Handle(null!, CancellationToken.None));
    }
}
