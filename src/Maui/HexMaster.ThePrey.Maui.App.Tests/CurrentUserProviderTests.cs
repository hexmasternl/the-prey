using HexMaster.ThePrey.Maui.App.Services.Api;
using HexMaster.ThePrey.Maui.App.Services.Authentication;
using HexMaster.ThePrey.Maui.App.Services.Session;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace HexMaster.ThePrey.Maui.App.Tests;

public class CurrentUserProviderTests
{
    private readonly Mock<IUserApiClient> _userApi = new();
    private readonly Mock<IAccessTokenProvider> _tokens = new();

    private CurrentUserProvider CreateSut() =>
        new(_userApi.Object, _tokens.Object, NullLogger<CurrentUserProvider>.Instance);

    private void SetupToken(string? token = "token") =>
        _tokens.Setup(t => t.GetAccessTokenAsync(It.IsAny<CancellationToken>())).ReturnsAsync(token);

    [Fact]
    public async Task GetUserIdAsync_ShouldReturnId_FromCurrentUser()
    {
        var id = Guid.NewGuid();
        SetupToken();
        _userApi.Setup(u => u.GetCurrentUserAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserSettingsResult.Success(new UserSettings("Alice", "en", id)));

        var result = await CreateSut().GetUserIdAsync();

        Assert.Equal(id, result);
    }

    [Fact]
    public async Task GetUserIdAsync_ShouldCache_AndCallServerOnce()
    {
        var id = Guid.NewGuid();
        SetupToken();
        _userApi.Setup(u => u.GetCurrentUserAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserSettingsResult.Success(new UserSettings("Alice", "en", id)));
        var sut = CreateSut();

        await sut.GetUserIdAsync();
        await sut.GetUserIdAsync();

        _userApi.Verify(u => u.GetCurrentUserAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetUserIdAsync_ShouldReturnNull_WhenNoToken()
    {
        SetupToken(null);

        Assert.Null(await CreateSut().GetUserIdAsync());
    }

    [Fact]
    public async Task GetUserIdAsync_ShouldInvalidateToken_OnUnauthorized()
    {
        SetupToken();
        _userApi.Setup(u => u.GetCurrentUserAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserSettingsResult.Unauthorized);

        var result = await CreateSut().GetUserIdAsync();

        Assert.Null(result);
        _tokens.Verify(t => t.Invalidate(), Times.Once);
    }
}
