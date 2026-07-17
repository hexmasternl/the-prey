using HexMaster.ThePrey.Maui.App.Services;
using HexMaster.ThePrey.Maui.App.Services.Authentication;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace HexMaster.ThePrey.Maui.App.Tests;

public class AccessTokenProviderTests
{
    private readonly Mock<ITokenStore> _tokenStore = new();
    private readonly Mock<IAuth0TokenClient> _auth0 = new();

    private AccessTokenProvider CreateSut() =>
        new(_tokenStore.Object, _auth0.Object, NullLogger<AccessTokenProvider>.Instance);

    [Fact]
    public async Task GetAccessTokenAsync_ShouldReturnToken_WhenRefreshTokenExchanges()
    {
        _tokenStore.Setup(s => s.GetRefreshTokenAsync()).ReturnsAsync("refresh");
        _auth0.Setup(a => a.RefreshAsync("refresh", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Auth0TokenResult.FromSuccess("access", "refresh"));

        var sut = CreateSut();
        var token = await sut.GetAccessTokenAsync();

        Assert.Equal("access", token);
    }

    [Fact]
    public async Task GetAccessTokenAsync_ShouldCacheToken_AndNotReExchangeOnSecondCall()
    {
        _tokenStore.Setup(s => s.GetRefreshTokenAsync()).ReturnsAsync("refresh");
        _auth0.Setup(a => a.RefreshAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Auth0TokenResult.FromSuccess("access", "refresh"));

        var sut = CreateSut();
        var first = await sut.GetAccessTokenAsync();
        var second = await sut.GetAccessTokenAsync();

        Assert.Equal("access", first);
        Assert.Equal("access", second);
        _auth0.Verify(a => a.RefreshAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAccessTokenAsync_ShouldReturnNull_WhenNoRefreshTokenStored()
    {
        _tokenStore.Setup(s => s.GetRefreshTokenAsync()).ReturnsAsync((string?)null);

        var sut = CreateSut();
        var token = await sut.GetAccessTokenAsync();

        Assert.Null(token);
        _auth0.Verify(a => a.RefreshAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetAccessTokenAsync_ShouldReturnNullAndClearToken_WhenExchangeRejected()
    {
        _tokenStore.Setup(s => s.GetRefreshTokenAsync()).ReturnsAsync("refresh");
        _auth0.Setup(a => a.RefreshAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Auth0TokenResult.Rejected);

        var sut = CreateSut();
        var token = await sut.GetAccessTokenAsync();

        Assert.Null(token);
        _tokenStore.Verify(s => s.ClearRefreshToken(), Times.Once);
    }

    [Fact]
    public async Task GetAccessTokenAsync_ShouldReturnNull_WhenExchangeTransientlyFails()
    {
        _tokenStore.Setup(s => s.GetRefreshTokenAsync()).ReturnsAsync("refresh");
        _auth0.Setup(a => a.RefreshAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Auth0TokenResult.Transient);

        var sut = CreateSut();
        var token = await sut.GetAccessTokenAsync();

        Assert.Null(token);
        _tokenStore.Verify(s => s.ClearRefreshToken(), Times.Never);
    }

    [Fact]
    public async Task GetAccessTokenAsync_ShouldPersistRotatedRefreshToken()
    {
        _tokenStore.Setup(s => s.GetRefreshTokenAsync()).ReturnsAsync("old-refresh");
        _auth0.Setup(a => a.RefreshAsync("old-refresh", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Auth0TokenResult.FromSuccess("access", "new-refresh"));

        var sut = CreateSut();
        await sut.GetAccessTokenAsync();

        _tokenStore.Verify(s => s.SetRefreshTokenAsync("new-refresh"), Times.Once);
    }

    [Fact]
    public async Task GetAccessTokenAsync_ShouldNotPersist_WhenRefreshTokenUnchanged()
    {
        _tokenStore.Setup(s => s.GetRefreshTokenAsync()).ReturnsAsync("refresh");
        _auth0.Setup(a => a.RefreshAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Auth0TokenResult.FromSuccess("access", "refresh"));

        var sut = CreateSut();
        await sut.GetAccessTokenAsync();

        _tokenStore.Verify(s => s.SetRefreshTokenAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task SetAccessToken_ShouldSeedCache_SoNoRefreshExchangeHappens()
    {
        // Mirrors interactive login priming the cache with the token from its code exchange: the next call
        // must return that token without spending (and rotating) the freshly-stored refresh token.
        _tokenStore.Setup(s => s.GetRefreshTokenAsync()).ReturnsAsync("refresh");

        var sut = CreateSut();
        sut.SetAccessToken("seeded");
        var token = await sut.GetAccessTokenAsync();

        Assert.Equal("seeded", token);
        _auth0.Verify(a => a.RefreshAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SetAccessToken_ShouldIgnoreBlankToken()
    {
        _tokenStore.Setup(s => s.GetRefreshTokenAsync()).ReturnsAsync("refresh");
        _auth0.Setup(a => a.RefreshAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Auth0TokenResult.FromSuccess("access", "refresh"));

        var sut = CreateSut();
        sut.SetAccessToken("   ");
        var token = await sut.GetAccessTokenAsync();

        // A blank seed must not poison the cache — the provider falls back to the refresh exchange.
        Assert.Equal("access", token);
    }

    [Fact]
    public async Task Invalidate_ShouldForceReExchange_OnNextCall()
    {
        _tokenStore.Setup(s => s.GetRefreshTokenAsync()).ReturnsAsync("refresh");
        _auth0.Setup(a => a.RefreshAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Auth0TokenResult.FromSuccess("access", "refresh"));

        var sut = CreateSut();
        await sut.GetAccessTokenAsync();
        sut.Invalidate();
        await sut.GetAccessTokenAsync();

        _auth0.Verify(a => a.RefreshAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }
}
