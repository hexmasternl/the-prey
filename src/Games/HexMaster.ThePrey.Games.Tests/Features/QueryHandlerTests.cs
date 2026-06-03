using HexMaster.ThePrey.Games.DomainModels;
using HexMaster.ThePrey.Games.Features.GetGame;
using HexMaster.ThePrey.Games.Features.ListGames;
using HexMaster.ThePrey.Games.Tests.Factories;
using Moq;

namespace HexMaster.ThePrey.Games.Tests.Features;

public sealed class QueryHandlerTests
{
    private readonly Mock<IGameRepository> _repository = new();

    [Fact]
    public async Task GetGame_ShouldReturnDto_WhenGameExists()
    {
        var game = GameFaker.LobbyGame();
        _repository.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);
        var handler = new GetGameQueryHandler(_repository.Object);

        var result = await handler.Handle(new GetGameQuery(game.Id), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(game.Id, result!.Id);
    }

    [Fact]
    public async Task GetGame_ShouldReturnNull_WhenGameNotFound()
    {
        _repository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Game?)null);
        var handler = new GetGameQueryHandler(_repository.Object);

        var result = await handler.Handle(new GetGameQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task ListGames_ShouldReturnSummaries_ForTheUser()
    {
        var userId = Guid.NewGuid();
        var games = new List<Game> { GameFaker.LobbyGame(ownerId: userId), GameFaker.LobbyGame(ownerId: userId) };
        _repository.Setup(r => r.ListForUserAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(games);
        var handler = new ListGamesQueryHandler(_repository.Object);

        var result = await handler.Handle(new ListGamesQuery(userId), CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.All(result, summary => Assert.Equal(userId, summary.OwnerUserId));
    }
}
