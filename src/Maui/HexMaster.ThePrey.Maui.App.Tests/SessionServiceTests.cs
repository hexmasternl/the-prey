using HexMaster.ThePrey.Maui.App.Services;
using HexMaster.ThePrey.Maui.App.Services.Api;
using HexMaster.ThePrey.Maui.App.Services.Authentication;
using HexMaster.ThePrey.Maui.App.Services.Session;
using Moq;

namespace HexMaster.ThePrey.Maui.App.Tests;

public class SessionServiceTests
{
    private readonly Mock<ITokenStore> _tokenStore = new();
    private readonly Mock<IAuth0TokenClient> _auth0 = new();
    private readonly Mock<IGameApiClient> _gameApi = new();

    private SessionService CreateSut() => new(_tokenStore.Object, _auth0.Object, _gameApi.Object);

    [Fact]
    public async Task TryEstablishSessionAsync_ShouldReturnUnauthenticated_WhenNoRefreshTokenStored()
    {
        _tokenStore.Setup(s => s.GetRefreshTokenAsync()).ReturnsAsync((string?)null);

        var result = await CreateSut().TryEstablishSessionAsync();

        Assert.Equal(SessionOutcome.Unauthenticated, result.Outcome);
        _auth0.Verify(a => a.RefreshAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TryEstablishSessionAsync_ShouldReturnActiveGame_WhenRefreshSucceedsAndBackendHasGame()
    {
        var game = new GameStatus { GameId = Guid.NewGuid(), PlayfieldName = "Downtown" };
        _tokenStore.Setup(s => s.GetRefreshTokenAsync()).ReturnsAsync("rt");
        _auth0.Setup(a => a.RefreshAsync("rt", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Auth0TokenResult.FromSuccess("access", "rt"));
        _gameApi.Setup(g => g.GetActiveGameAsync("access", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ActiveGameResult.Active(game));

        var result = await CreateSut().TryEstablishSessionAsync();

        Assert.Equal(SessionOutcome.ActiveGame, result.Outcome);
        Assert.Same(game, result.Game);
    }

    [Fact]
    public async Task TryEstablishSessionAsync_ShouldReturnNoActiveGame_WhenBackendReturns404()
    {
        _tokenStore.Setup(s => s.GetRefreshTokenAsync()).ReturnsAsync("rt");
        _auth0.Setup(a => a.RefreshAsync("rt", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Auth0TokenResult.FromSuccess("access", null));
        _gameApi.Setup(g => g.GetActiveGameAsync("access", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ActiveGameResult.None);

        var result = await CreateSut().TryEstablishSessionAsync();

        Assert.Equal(SessionOutcome.NoActiveGame, result.Outcome);
    }

    [Fact]
    public async Task TryEstablishSessionAsync_ShouldClearTokenAndReturnUnauthenticated_WhenRefreshRejected()
    {
        _tokenStore.Setup(s => s.GetRefreshTokenAsync()).ReturnsAsync("rt");
        _auth0.Setup(a => a.RefreshAsync("rt", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Auth0TokenResult.Rejected);

        var result = await CreateSut().TryEstablishSessionAsync();

        Assert.Equal(SessionOutcome.Unauthenticated, result.Outcome);
        _tokenStore.Verify(s => s.ClearRefreshToken(), Times.Once);
        _gameApi.Verify(g => g.GetActiveGameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TryEstablishSessionAsync_ShouldNotClearToken_WhenRefreshTransientlyFails()
    {
        _tokenStore.Setup(s => s.GetRefreshTokenAsync()).ReturnsAsync("rt");
        _auth0.Setup(a => a.RefreshAsync("rt", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Auth0TokenResult.Transient);

        var result = await CreateSut().TryEstablishSessionAsync();

        Assert.Equal(SessionOutcome.Unauthenticated, result.Outcome);
        _tokenStore.Verify(s => s.ClearRefreshToken(), Times.Never);
    }

    [Fact]
    public async Task TryEstablishSessionAsync_ShouldReturnUnauthenticated_WhenBackendRejectsAccessToken()
    {
        _tokenStore.Setup(s => s.GetRefreshTokenAsync()).ReturnsAsync("rt");
        _auth0.Setup(a => a.RefreshAsync("rt", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Auth0TokenResult.FromSuccess("access", null));
        _gameApi.Setup(g => g.GetActiveGameAsync("access", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ActiveGameResult.Unauthorized);

        var result = await CreateSut().TryEstablishSessionAsync();

        Assert.Equal(SessionOutcome.Unauthenticated, result.Outcome);
    }

    [Fact]
    public async Task TryEstablishSessionAsync_ShouldPersistRotatedRefreshToken_WhenAuth0ReturnsNewOne()
    {
        _tokenStore.Setup(s => s.GetRefreshTokenAsync()).ReturnsAsync("old-rt");
        _auth0.Setup(a => a.RefreshAsync("old-rt", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Auth0TokenResult.FromSuccess("access", "new-rt"));
        _gameApi.Setup(g => g.GetActiveGameAsync("access", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ActiveGameResult.None);

        await CreateSut().TryEstablishSessionAsync();

        _tokenStore.Verify(s => s.SetRefreshTokenAsync("new-rt"), Times.Once);
    }

    [Fact]
    public async Task TryEstablishSessionAsync_ShouldNotRewriteRefreshToken_WhenUnchanged()
    {
        _tokenStore.Setup(s => s.GetRefreshTokenAsync()).ReturnsAsync("rt");
        _auth0.Setup(a => a.RefreshAsync("rt", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Auth0TokenResult.FromSuccess("access", "rt"));
        _gameApi.Setup(g => g.GetActiveGameAsync("access", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ActiveGameResult.None);

        await CreateSut().TryEstablishSessionAsync();

        _tokenStore.Verify(s => s.SetRefreshTokenAsync(It.IsAny<string>()), Times.Never);
    }
}
