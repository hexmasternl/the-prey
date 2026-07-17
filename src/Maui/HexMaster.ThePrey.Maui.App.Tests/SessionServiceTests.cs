using HexMaster.ThePrey.Maui.App.Services.Api;
using HexMaster.ThePrey.Maui.App.Services.Authentication;
using HexMaster.ThePrey.Maui.App.Services.Session;
using Moq;

namespace HexMaster.ThePrey.Maui.App.Tests;

public class SessionServiceTests
{
    private readonly Mock<IAccessTokenProvider> _accessTokenProvider = new();
    private readonly Mock<IGameApiClient> _gameApi = new();

    private SessionService CreateSut() => new(_accessTokenProvider.Object, _gameApi.Object);

    [Fact]
    public async Task TryEstablishSessionAsync_ShouldReturnUnauthenticated_WhenNoAccessToken()
    {
        // A null token means the provider had no valid refresh token (or the exchange failed); either way
        // there is no session and we must not attempt an active-game lookup.
        _accessTokenProvider.Setup(p => p.GetAccessTokenAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var result = await CreateSut().TryEstablishSessionAsync();

        Assert.Equal(SessionOutcome.Unauthenticated, result.Outcome);
        _gameApi.Verify(g => g.GetActiveGameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TryEstablishSessionAsync_ShouldReturnActiveGame_WhenTokenResolvesAndBackendHasGame()
    {
        var game = new GameStatus { GameId = Guid.NewGuid(), PlayfieldName = "Downtown" };
        _accessTokenProvider.Setup(p => p.GetAccessTokenAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("access");
        _gameApi.Setup(g => g.GetActiveGameAsync("access", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ActiveGameResult.Active(game));

        var result = await CreateSut().TryEstablishSessionAsync();

        Assert.Equal(SessionOutcome.ActiveGame, result.Outcome);
        Assert.Same(game, result.Game);
    }

    [Fact]
    public async Task TryEstablishSessionAsync_ShouldReturnNoActiveGame_WhenBackendReturns404()
    {
        _accessTokenProvider.Setup(p => p.GetAccessTokenAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("access");
        _gameApi.Setup(g => g.GetActiveGameAsync("access", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ActiveGameResult.None);

        var result = await CreateSut().TryEstablishSessionAsync();

        Assert.Equal(SessionOutcome.NoActiveGame, result.Outcome);
    }

    [Fact]
    public async Task TryEstablishSessionAsync_ShouldReturnUnauthenticated_WhenBackendRejectsAccessToken()
    {
        _accessTokenProvider.Setup(p => p.GetAccessTokenAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("access");
        _gameApi.Setup(g => g.GetActiveGameAsync("access", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ActiveGameResult.Unauthorized);

        var result = await CreateSut().TryEstablishSessionAsync();

        Assert.Equal(SessionOutcome.Unauthenticated, result.Outcome);
    }
}
