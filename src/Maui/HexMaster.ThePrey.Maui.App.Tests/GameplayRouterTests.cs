using HexMaster.ThePrey.Maui.App.Services.Api;
using HexMaster.ThePrey.Maui.App.Services.Authentication;
using HexMaster.ThePrey.Maui.App.Services.Navigation;
using HexMaster.ThePrey.Maui.App.Services.Session;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace HexMaster.ThePrey.Maui.App.Tests;

public class GameplayRouterTests
{
    private readonly Mock<IGameApiClient> _api = new();
    private readonly Mock<IAccessTokenProvider> _tokens = new();
    private readonly Mock<ICurrentUserProvider> _currentUser = new();
    private readonly Mock<IMenuNavigator> _nav = new();
    private readonly Guid _gameId = Guid.NewGuid();

    private GameplayRouter CreateSut() => new(
        _api.Object, _tokens.Object, _currentUser.Object, _nav.Object, NullLogger<GameplayRouter>.Instance);

    private static GameDetails Game(Guid? hunterUserId) => new(
        Guid.NewGuid(), "1234", "InProgress",
        new GameConfigurationDetails(30, 5, 10, 120, 60),
        Array.Empty<GameParticipantDetails>(),
        hunterUserId, OwnerUserId: Guid.NewGuid(), IsOwnerPlayer: false, IsReadyToStart: false);

    private void ArrangeResolvableGame(Guid hunterUserId, Guid currentUserId)
    {
        _tokens.Setup(t => t.GetAccessTokenAsync(It.IsAny<CancellationToken>())).ReturnsAsync("token");
        _api.Setup(a => a.GetActiveGameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ActiveGameResult.Active(new GameStatus { GameId = _gameId }));
        _api.Setup(a => a.GetGameAsync(_gameId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GetGameResult.Success(Game(hunterUserId)));
        _currentUser.Setup(c => c.GetUserIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(currentUserId);
    }

    [Fact]
    public async Task GoToGameplayAsync_ShouldRouteToHunterPage_WhenCallerIsHunter()
    {
        var me = Guid.NewGuid();
        ArrangeResolvableGame(hunterUserId: me, currentUserId: me);

        await CreateSut().GoToGameplayAsync();

        _nav.Verify(n => n.GoToAsync(GameplayRouter.HunterGameRoute), Times.Once);
        _nav.Verify(n => n.GoToAsync(GameplayRouter.PreyGameRoute), Times.Never);
    }

    [Fact]
    public async Task GoToGameplayAsync_ShouldRouteToPreyPage_WhenCallerIsNotHunter()
    {
        ArrangeResolvableGame(hunterUserId: Guid.NewGuid(), currentUserId: Guid.NewGuid());

        await CreateSut().GoToGameplayAsync();

        _nav.Verify(n => n.GoToAsync(GameplayRouter.PreyGameRoute), Times.Once);
        _nav.Verify(n => n.GoToAsync(GameplayRouter.HunterGameRoute), Times.Never);
    }

    [Fact]
    public async Task GoToGameplayAsync_ShouldDefaultToPreyPage_WhenNoToken()
    {
        _tokens.Setup(t => t.GetAccessTokenAsync(It.IsAny<CancellationToken>())).ReturnsAsync((string?)null);

        await CreateSut().GoToGameplayAsync();

        _nav.Verify(n => n.GoToAsync(GameplayRouter.PreyGameRoute), Times.Once);
    }

    [Fact]
    public async Task GoToGameplayAsync_ShouldDefaultToPreyPage_WhenNoActiveGame()
    {
        _tokens.Setup(t => t.GetAccessTokenAsync(It.IsAny<CancellationToken>())).ReturnsAsync("token");
        _api.Setup(a => a.GetActiveGameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ActiveGameResult.None);

        await CreateSut().GoToGameplayAsync();

        _nav.Verify(n => n.GoToAsync(GameplayRouter.PreyGameRoute), Times.Once);
    }

    [Fact]
    public async Task GoToGameplayAsync_ShouldDefaultToPreyPage_WhenHunterUnknownOrIdUnavailable()
    {
        _tokens.Setup(t => t.GetAccessTokenAsync(It.IsAny<CancellationToken>())).ReturnsAsync("token");
        _api.Setup(a => a.GetActiveGameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ActiveGameResult.Active(new GameStatus { GameId = _gameId }));
        _api.Setup(a => a.GetGameAsync(_gameId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GetGameResult.Success(Game(hunterUserId: Guid.NewGuid())));
        _currentUser.Setup(c => c.GetUserIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync((Guid?)null);

        await CreateSut().GoToGameplayAsync();

        _nav.Verify(n => n.GoToAsync(GameplayRouter.PreyGameRoute), Times.Once);
    }

    [Fact]
    public async Task GoToOutcomeAsync_ShouldNavigateToOutcomeRoute()
    {
        await CreateSut().GoToOutcomeAsync();

        _nav.Verify(n => n.GoToAsync(GameplayRouter.OutcomeRoute), Times.Once);
    }
}
