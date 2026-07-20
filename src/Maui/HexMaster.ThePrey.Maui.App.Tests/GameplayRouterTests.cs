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
    private readonly Mock<IOutcomeNavigator> _outcomeNav = new();
    private readonly Guid _gameId = Guid.NewGuid();

    private GameplayRouter CreateSut() => new(
        _api.Object, _tokens.Object, _currentUser.Object, _nav.Object, _outcomeNav.Object,
        NullLogger<GameplayRouter>.Instance);

    private static GameDetails Game(Guid? hunterUserId, string status = "InProgress") => new(
        Guid.NewGuid(), "1234", status,
        new GameConfigurationDetails(30, 5, 10, 120, 60),
        Array.Empty<GameParticipantDetails>(),
        hunterUserId, OwnerUserId: Guid.NewGuid(), IsOwnerPlayer: false, IsReadyToStart: false);

    private void ArrangeResolvableGame(Guid hunterUserId, Guid currentUserId, string status = "InProgress")
    {
        _tokens.Setup(t => t.GetAccessTokenAsync(It.IsAny<CancellationToken>())).ReturnsAsync("token");
        _api.Setup(a => a.GetActiveGameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ActiveGameResult.Active(new GameStatus { GameId = _gameId }));
        _api.Setup(a => a.GetGameAsync(_gameId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GetGameResult.Success(Game(hunterUserId, status)));
        _currentUser.Setup(c => c.GetUserIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(currentUserId);
    }

    // Resume from the main menu: same role branch as the lobby hand-off, but it must PUSH rather than
    // replace — the menu is the Shell root, so ReplaceAsync's leading ".." would have nothing to pop.
    [Theory]
    [InlineData("Started")]
    [InlineData("InProgress")]
    public async Task ResumeGameplayAsync_ShouldPushHunterPage_WhenCallerIsHunter(string status)
    {
        var me = Guid.NewGuid();
        ArrangeResolvableGame(hunterUserId: me, currentUserId: me, status: status);

        await CreateSut().ResumeGameplayAsync();

        _nav.Verify(n => n.GoToAsync(GameplayRouter.HunterGameRoute), Times.Once);
        _nav.Verify(n => n.ReplaceAsync(It.IsAny<string>()), Times.Never);
    }

    [Theory]
    [InlineData("Started")]
    [InlineData("InProgress")]
    public async Task ResumeGameplayAsync_ShouldPushPreyPage_WhenCallerIsNotHunter(string status)
    {
        ArrangeResolvableGame(hunterUserId: Guid.NewGuid(), currentUserId: Guid.NewGuid(), status: status);

        await CreateSut().ResumeGameplayAsync();

        _nav.Verify(n => n.GoToAsync(GameplayRouter.PreyGameRoute), Times.Once);
        _nav.Verify(n => n.ReplaceAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GoToGameplayAsync_ShouldRouteToHunterPage_WhenCallerIsHunter()
    {
        var me = Guid.NewGuid();
        ArrangeResolvableGame(hunterUserId: me, currentUserId: me);

        await CreateSut().GoToGameplayAsync();

        _nav.Verify(n => n.ReplaceAsync(GameplayRouter.HunterGameRoute), Times.Once);
        _nav.Verify(n => n.ReplaceAsync(GameplayRouter.PreyGameRoute), Times.Never);
    }

    // A game the owner has just armed is Started, not yet InProgress — the sweep has not run. The role
    // branch must work for the whole Started window, not only once play is committed.
    [Theory]
    [InlineData("Started")]
    [InlineData("InProgress")]
    public async Task GoToGameplayAsync_ShouldRouteToHunterPage_WhenCallerIsHunterAndGameIsArmed(string status)
    {
        var me = Guid.NewGuid();
        ArrangeResolvableGame(hunterUserId: me, currentUserId: me, status: status);

        await CreateSut().GoToGameplayAsync();

        _nav.Verify(n => n.ReplaceAsync(GameplayRouter.HunterGameRoute), Times.Once);
        _nav.Verify(n => n.ReplaceAsync(GameplayRouter.PreyGameRoute), Times.Never);
    }

    // The hand-off is one-way: the lobby of an already-started game must not remain on the stack.
    [Fact]
    public async Task GoToGameplayAsync_ShouldReplaceCurrentPage_RatherThanPushOntoTheStack()
    {
        var me = Guid.NewGuid();
        ArrangeResolvableGame(hunterUserId: me, currentUserId: me, status: "Started");

        await CreateSut().GoToGameplayAsync();

        _nav.Verify(n => n.GoToAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GoToGameplayAsync_ShouldRouteToPreyPage_WhenCallerIsNotHunter()
    {
        ArrangeResolvableGame(hunterUserId: Guid.NewGuid(), currentUserId: Guid.NewGuid());

        await CreateSut().GoToGameplayAsync();

        _nav.Verify(n => n.ReplaceAsync(GameplayRouter.PreyGameRoute), Times.Once);
        _nav.Verify(n => n.ReplaceAsync(GameplayRouter.HunterGameRoute), Times.Never);
    }

    [Fact]
    public async Task GoToGameplayAsync_ShouldDefaultToPreyPage_WhenNoToken()
    {
        _tokens.Setup(t => t.GetAccessTokenAsync(It.IsAny<CancellationToken>())).ReturnsAsync((string?)null);

        await CreateSut().GoToGameplayAsync();

        _nav.Verify(n => n.ReplaceAsync(GameplayRouter.PreyGameRoute), Times.Once);
    }

    [Fact]
    public async Task GoToGameplayAsync_ShouldDefaultToPreyPage_WhenNoActiveGame()
    {
        _tokens.Setup(t => t.GetAccessTokenAsync(It.IsAny<CancellationToken>())).ReturnsAsync("token");
        _api.Setup(a => a.GetActiveGameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ActiveGameResult.None);

        await CreateSut().GoToGameplayAsync();

        _nav.Verify(n => n.ReplaceAsync(GameplayRouter.PreyGameRoute), Times.Once);
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

        _nav.Verify(n => n.ReplaceAsync(GameplayRouter.PreyGameRoute), Times.Once);
    }

    [Fact]
    public async Task GoToOutcomeAsync_ShouldDelegateToOutcomeNavigator_WithGameAndRole()
    {
        await CreateSut().GoToOutcomeAsync(_gameId, isHunter: true);

        _outcomeNav.Verify(n => n.GoToOutcomeAsync(_gameId, true), Times.Once);
        _nav.Verify(n => n.GoToAsync(It.IsAny<string>()), Times.Never);
    }
}
