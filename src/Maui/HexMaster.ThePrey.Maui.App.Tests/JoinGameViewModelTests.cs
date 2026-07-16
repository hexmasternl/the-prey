using HexMaster.ThePrey.Maui.App.Services.Api;
using HexMaster.ThePrey.Maui.App.Services.Authentication;
using HexMaster.ThePrey.Maui.App.Services.Localization;
using HexMaster.ThePrey.Maui.App.Services.Navigation;
using HexMaster.ThePrey.Maui.App.Services.Session;
using HexMaster.ThePrey.Maui.App.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace HexMaster.ThePrey.Maui.App.Tests;

public class JoinGameViewModelTests
{
    private const string DefaultDisplayName = "Player";

    private readonly Mock<IGameApiClient> _gameApi = new();
    private readonly Mock<IUserApiClient> _userApi = new();
    private readonly Mock<IAccessTokenProvider> _tokenProvider = new();
    private readonly Mock<IInteractiveLoginService> _login = new();
    private readonly Mock<ISessionService> _session = new();
    private readonly Mock<IMenuNavigator> _navigator = new();
    private readonly Mock<ILocalizationService> _localization = new();

    private readonly Guid _gameId = Guid.NewGuid();

    public JoinGameViewModelTests()
    {
        _tokenProvider.Setup(t => t.GetAccessTokenAsync(It.IsAny<CancellationToken>())).ReturnsAsync("token");
        _userApi.Setup(u => u.GetCurrentUserAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserSettingsResult.Success(new UserSettings("Alice", "en")));
        _gameApi.Setup(g => g.JoinGameAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(JoinGameResult.Success(new GameSummary(Guid.NewGuid())));
        _login.Setup(l => l.LoginAsync(It.IsAny<CancellationToken>())).ReturnsAsync(InteractiveLoginOutcome.Success);
        _session.Setup(s => s.TryEstablishSessionAsync(It.IsAny<CancellationToken>())).ReturnsAsync(SessionResult.NoGame);
        _navigator.Setup(n => n.GoToAsync(It.IsAny<string>())).Returns(Task.CompletedTask);
        _localization.Setup(l => l[StartGameViewModel.DefaultDisplayNameKey]).Returns(DefaultDisplayName);
        _localization.Setup(l => l[It.IsRegex("^Join_Conflict_")]).Returns<string>(k => k);
    }

    private JoinGameViewModel CreateSut() => new(
        _gameApi.Object, _userApi.Object, _tokenProvider.Object, _login.Object, _session.Object,
        _navigator.Object, _localization.Object, NullLogger<JoinGameViewModel>.Instance);

    private JoinGameViewModel WithGameId(JoinGameViewModel sut, Guid? id = null)
    {
        sut.SetPendingGame(id ?? _gameId);
        return sut;
    }

    // A signed-in VM with the game id set and a complete code (ready to join).
    private async Task<JoinGameViewModel> ReadyToJoinAsync()
    {
        var sut = WithGameId(CreateSut());
        await sut.OnAppearingAsync();
        sut.Code = "1234";
        return sut;
    }

    // ---- Query attributes ----

    [Fact]
    public async Task Join_ShouldUseTheGameIdFromTheQuery()
    {
        var sut = await ReadyToJoinAsync();
        Guid? captured = null;
        _gameApi.Setup(g => g.JoinGameAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, string, string, string, CancellationToken>((id, _, _, _, _) => captured = id)
            .ReturnsAsync(JoinGameResult.Success(new GameSummary(Guid.NewGuid())));

        await sut.JoinAsync();

        Assert.Equal(_gameId, captured);
    }

    // ---- Sign-in gate ----

    [Fact]
    public async Task OnAppearing_ShouldBeSignedOut_WhenNoToken()
    {
        _tokenProvider.Setup(t => t.GetAccessTokenAsync(It.IsAny<CancellationToken>())).ReturnsAsync((string?)null);
        var sut = WithGameId(CreateSut());

        await sut.OnAppearingAsync();

        Assert.False(sut.IsSignedIn);
        Assert.True(sut.IsSignedOut);
    }

    [Fact]
    public async Task OnAppearing_ShouldBeSignedIn_WhenTokenPresent()
    {
        var sut = WithGameId(CreateSut());

        await sut.OnAppearingAsync();

        Assert.True(sut.IsSignedIn);
        Assert.False(sut.IsSignedOut);
    }

    [Fact]
    public async Task OnAppearing_ShouldDriveLoginAutomatically_WhenSignedOut()
    {
        // Landing on the join page signed out must run the login procedure straight away, then show the
        // code entry on success — the user should not have to tap a sign-in button first.
        _tokenProvider.SetupSequence(t => t.GetAccessTokenAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null)   // OnAppearing → signed out
            .ReturnsAsync("token");        // after the auto-login → signed in
        var sut = WithGameId(CreateSut());

        await sut.OnAppearingAsync();

        _login.Verify(l => l.LoginAsync(It.IsAny<CancellationToken>()), Times.Once);
        Assert.True(sut.IsSignedIn);
    }

    [Fact]
    public async Task OnAppearing_ShouldNotDriveLogin_WhenAlreadySignedIn()
    {
        var sut = WithGameId(CreateSut());

        await sut.OnAppearingAsync();

        _login.Verify(l => l.LoginAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task LogIn_ShouldContinueSignedIn_OnSuccess()
    {
        // Signed out first, then the login establishes a session and a token becomes available.
        _tokenProvider.SetupSequence(t => t.GetAccessTokenAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null)   // OnAppearing → signed out
            .ReturnsAsync("token");        // after login → signed in
        var sut = WithGameId(CreateSut());
        await sut.OnAppearingAsync();

        await sut.LogInAsync();

        Assert.True(sut.IsSignedIn);
        _session.Verify(s => s.TryEstablishSessionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task LogIn_ShouldStaySignedOut_AndKeepGameId_OnCancel()
    {
        _tokenProvider.Setup(t => t.GetAccessTokenAsync(It.IsAny<CancellationToken>())).ReturnsAsync((string?)null);
        _login.Setup(l => l.LoginAsync(It.IsAny<CancellationToken>())).ReturnsAsync(InteractiveLoginOutcome.Cancelled);
        var sut = WithGameId(CreateSut());
        await sut.OnAppearingAsync();

        await sut.LogInAsync();

        Assert.True(sut.IsSignedOut);

        // The pending id survived: once a token is available and a complete code is entered, the join uses it.
        _tokenProvider.Setup(t => t.GetAccessTokenAsync(It.IsAny<CancellationToken>())).ReturnsAsync("token");
        await sut.OnAppearingAsync();
        sut.Code = "1234";
        Guid? captured = null;
        _gameApi.Setup(g => g.JoinGameAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, string, string, string, CancellationToken>((id, _, _, _, _) => captured = id)
            .ReturnsAsync(JoinGameResult.Success(new GameSummary(Guid.NewGuid())));

        await sut.JoinAsync();

        Assert.Equal(_gameId, captured);
    }

    // ---- Code entry ----

    [Fact]
    public void Code_ShouldRejectNonDigits_AndCapAtFour()
    {
        var sut = CreateSut();

        sut.Code = "12ab34567";

        Assert.Equal("1234", sut.Code);
        Assert.True(sut.IsCodeComplete);
    }

    [Fact]
    public void Code_ShouldNotBeComplete_BelowFourDigits()
    {
        var sut = CreateSut();

        sut.Code = "12";

        Assert.False(sut.IsCodeComplete);
    }

    // ---- CanJoin ----

    [Fact]
    public async Task CanJoin_ShouldBeFalse_UntilCodeIsComplete()
    {
        var sut = WithGameId(CreateSut());
        await sut.OnAppearingAsync();

        sut.Code = "123";
        Assert.False(sut.CanJoin);

        sut.Code = "1234";
        Assert.True(sut.CanJoin);
    }

    [Fact]
    public void CanJoin_ShouldBeFalse_WhenSignedOut()
    {
        var sut = CreateSut();

        sut.Code = "1234";

        Assert.False(sut.IsSignedIn);
        Assert.False(sut.CanJoin);
    }

    // ---- Display name sourcing ----

    [Fact]
    public async Task Join_ShouldUseProfileDisplayName()
    {
        var sut = await ReadyToJoinAsync();
        string? capturedName = null;
        _gameApi.Setup(g => g.JoinGameAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, string, string, string, CancellationToken>((_, _, name, _, _) => capturedName = name)
            .ReturnsAsync(JoinGameResult.Success(new GameSummary(Guid.NewGuid())));

        await sut.JoinAsync();

        Assert.Equal("Alice", capturedName);
    }

    [Fact]
    public async Task Join_ShouldFallBackToDefaultDisplayName_WhenProfileNotFound()
    {
        var sut = await ReadyToJoinAsync();
        _userApi.Setup(u => u.GetCurrentUserAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserSettingsResult.NotFound);
        string? capturedName = null;
        _gameApi.Setup(g => g.JoinGameAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, string, string, string, CancellationToken>((_, _, name, _, _) => capturedName = name)
            .ReturnsAsync(JoinGameResult.Success(new GameSummary(Guid.NewGuid())));

        await sut.JoinAsync();

        Assert.Equal(DefaultDisplayName, capturedName);
    }

    [Fact]
    public async Task Join_ShouldSignOut_AndInvalidateToken_WhenProfileReadUnauthorized()
    {
        var sut = await ReadyToJoinAsync();
        _userApi.Setup(u => u.GetCurrentUserAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserSettingsResult.Unauthorized);

        await sut.JoinAsync();

        _tokenProvider.Verify(t => t.Invalidate(), Times.Once);
        Assert.True(sut.IsSignedOut);
        _gameApi.Verify(g => g.JoinGameAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ---- Join result mapping ----

    [Fact]
    public async Task Join_ShouldNavigateToGame_WithJoinedId_OnSuccess()
    {
        var joinedId = Guid.NewGuid();
        var sut = await ReadyToJoinAsync();
        _gameApi.Setup(g => g.JoinGameAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(JoinGameResult.Success(new GameSummary(joinedId)));

        await sut.JoinAsync();

        _navigator.Verify(n => n.GoToAsync(
            $"../{MainMenuViewModel.GameRoute}?{GameLobbyViewModel.GameIdQueryKey}={joinedId}"), Times.Once);
    }

    [Fact]
    public async Task Join_ShouldShowInvalidCode_AndKeepCode_On400()
    {
        var sut = await ReadyToJoinAsync();
        _gameApi.Setup(g => g.JoinGameAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(JoinGameResult.InvalidCode("invalid_join_code"));

        await sut.JoinAsync();

        Assert.True(sut.HasInvalidCode);
        Assert.Equal("1234", sut.Code);
        _navigator.Verify(n => n.GoToAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Join_ShouldShowNotFound_On404()
    {
        var sut = await ReadyToJoinAsync();
        _gameApi.Setup(g => g.JoinGameAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(JoinGameResult.NotFound);

        await sut.JoinAsync();

        Assert.True(sut.HasNotFound);
    }

    [Fact]
    public async Task Join_ShouldShowConflictMessage_FromRuleCode_On409()
    {
        var sut = await ReadyToJoinAsync();
        _gameApi.Setup(g => g.JoinGameAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(JoinGameResult.Conflict("game_already_started"));

        await sut.JoinAsync();

        Assert.True(sut.HasConflict);
        Assert.Equal("Join_Conflict_AlreadyStarted", sut.ConflictMessage);
    }

    [Fact]
    public async Task Join_ShouldInvalidateToken_AndSignOut_On401()
    {
        var sut = await ReadyToJoinAsync();
        _gameApi.Setup(g => g.JoinGameAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(JoinGameResult.Unauthorized);

        await sut.JoinAsync();

        _tokenProvider.Verify(t => t.Invalidate(), Times.Once);
        Assert.True(sut.IsSignedOut);
    }

    [Fact]
    public async Task Join_ShouldShowError_OnTransientError()
    {
        var sut = await ReadyToJoinAsync();
        _gameApi.Setup(g => g.JoinGameAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(JoinGameResult.Error);

        await sut.JoinAsync();

        Assert.True(sut.HasError);
        _navigator.Verify(n => n.GoToAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Join_ShouldSignOut_WhenTokenMissingAtJoinTime()
    {
        var sut = await ReadyToJoinAsync();
        // The token disappears between the gate and the join (e.g. cleared elsewhere).
        _tokenProvider.Setup(t => t.GetAccessTokenAsync(It.IsAny<CancellationToken>())).ReturnsAsync((string?)null);

        await sut.JoinAsync();

        Assert.True(sut.IsSignedOut);
        _gameApi.Verify(g => g.JoinGameAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
