using HexMaster.ThePrey.Maui.App.Services;
using HexMaster.ThePrey.Maui.App.Services.Api;
using HexMaster.ThePrey.Maui.App.Services.Authentication;
using HexMaster.ThePrey.Maui.App.Services.Location;
using HexMaster.ThePrey.Maui.App.Services.Navigation;
using HexMaster.ThePrey.Maui.App.Services.Platform;
using HexMaster.ThePrey.Maui.App.Services.Session;
using HexMaster.ThePrey.Maui.App.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace HexMaster.ThePrey.Maui.App.Tests;

public class MainMenuViewModelTests
{
    private readonly Mock<ISessionService> _session = new();
    private readonly Mock<ITokenStore> _tokenStore = new();
    private readonly Mock<IInteractiveLoginService> _login = new();
    private readonly Mock<IUserApiClient> _userApi = new();
    private readonly Mock<IAccessTokenProvider> _accessToken = new();
    private readonly Mock<IMenuNavigator> _navigator = new();
    private readonly Mock<IApplicationExit> _app = new();
    private readonly Mock<IGpsReader> _gpsReader = new();
    private readonly Mock<IAppVersionProvider> _version = new();

    public MainMenuViewModelTests()
    {
        _app.SetupGet(a => a.IsExitSupported).Returns(true);
        _gpsReader.Setup(g => g.ReadAsync(It.IsAny<CancellationToken>())).ReturnsAsync((GpsFix?)null);
        _version.SetupGet(v => v.Version).Returns("1.0");
        // Default: no token, so the fire-and-forget player-name load is a no-op unless a test overrides it.
        _accessToken.Setup(a => a.GetAccessTokenAsync(It.IsAny<CancellationToken>())).ReturnsAsync((string?)null);
    }

    private MainMenuViewModel CreateSut() => new(
        _session.Object, _tokenStore.Object, _login.Object, _userApi.Object, _accessToken.Object,
        _navigator.Object, _app.Object, _gpsReader.Object, _version.Object,
        NullLogger<MainMenuViewModel>.Instance);

    private void SetupSession(SessionResult result) =>
        _session.Setup(s => s.TryEstablishSessionAsync(It.IsAny<CancellationToken>())).ReturnsAsync(result);

    [Fact]
    public async Task LoadStateAsync_ShouldShowResumeAndEnableSignedInActions_WhenActiveGame()
    {
        var game = new GameStatus { GameId = Guid.NewGuid() };
        SetupSession(SessionResult.Active(game));
        var sut = CreateSut();

        await sut.LoadStateAsync();

        Assert.True(sut.IsSignedIn);
        Assert.True(sut.HasActiveGame);
        Assert.False(sut.ShowLogIn);
        Assert.True(sut.ShowResume);
        Assert.False(sut.ShowStart);
        Assert.True(sut.CanUseSignedInActions);
        Assert.True(sut.ResumeGameCommand.CanExecute(null));
        Assert.False(sut.StartGameCommand.CanExecute(null));
        Assert.True(sut.PlayfieldsCommand.CanExecute(null));
        Assert.False(sut.LogInCommand.CanExecute(null));
    }

    [Fact]
    public async Task LoadStateAsync_ShouldShowStart_WhenNoActiveGame()
    {
        SetupSession(SessionResult.NoGame);
        var sut = CreateSut();

        await sut.LoadStateAsync();

        Assert.True(sut.IsSignedIn);
        Assert.False(sut.HasActiveGame);
        Assert.True(sut.ShowStart);
        Assert.False(sut.ShowResume);
        Assert.True(sut.StartGameCommand.CanExecute(null));
        Assert.False(sut.ResumeGameCommand.CanExecute(null));
        Assert.True(sut.LogOutCommand.CanExecute(null));
    }

    [Fact]
    public async Task LoadStateAsync_ShouldShowLogInAndDisableEverythingButLogInAndExit_WhenUnauthenticated()
    {
        SetupSession(SessionResult.Unauthenticated);
        var sut = CreateSut();

        await sut.LoadStateAsync();

        Assert.False(sut.IsSignedIn);
        Assert.True(sut.ShowLogIn);
        Assert.False(sut.ShowResume);
        Assert.False(sut.ShowStart);
        Assert.True(sut.LogInCommand.CanExecute(null));
        Assert.True(sut.ExitCommand.CanExecute(null));
        Assert.False(sut.PlayfieldsCommand.CanExecute(null));
        Assert.False(sut.SettingsCommand.CanExecute(null));
        Assert.False(sut.LogOutCommand.CanExecute(null));
        Assert.False(sut.ResumeGameCommand.CanExecute(null));
        Assert.False(sut.StartGameCommand.CanExecute(null));
    }

    [Fact]
    public async Task LoadStateAsync_ShouldKeepGameplayEntryDisabledWhileBusy_UntilCheckCompletes()
    {
        var tcs = new TaskCompletionSource<SessionResult>();
        _session.Setup(s => s.TryEstablishSessionAsync(It.IsAny<CancellationToken>())).Returns(tcs.Task);
        var sut = CreateSut();

        var loading = sut.LoadStateAsync();

        // Mid-flight: the check has not resolved yet.
        Assert.True(sut.IsBusy);
        Assert.False(sut.ResumeGameCommand.CanExecute(null));
        Assert.False(sut.StartGameCommand.CanExecute(null));
        Assert.False(sut.LogInCommand.CanExecute(null));
        Assert.True(sut.ExitCommand.CanExecute(null));

        tcs.SetResult(SessionResult.NoGame);
        await loading;

        Assert.False(sut.IsBusy);
        Assert.True(sut.ShowStart);
        Assert.True(sut.StartGameCommand.CanExecute(null));
    }

    [Fact]
    public async Task LogOutCommand_ShouldClearTokenAndReturnToSignedOut()
    {
        SetupSession(SessionResult.NoGame);
        var sut = CreateSut();
        await sut.LoadStateAsync();

        await RunAsync(sut.LogOutCommand);

        _tokenStore.Verify(t => t.ClearRefreshToken(), Times.Once);
        Assert.False(sut.IsSignedIn);
        Assert.True(sut.ShowLogIn);
        Assert.False(sut.LogOutCommand.CanExecute(null));
    }

    [Fact]
    public async Task LogInCommand_ShouldReEvaluateToSignedIn_WhenLoginSucceeds()
    {
        SetupSession(SessionResult.Unauthenticated);
        var sut = CreateSut();
        await sut.LoadStateAsync();

        _login.Setup(l => l.LoginAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(InteractiveLoginOutcome.Success);
        _session.Setup(s => s.TryEstablishSessionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(SessionResult.Active(new GameStatus { GameId = Guid.NewGuid() }));

        await RunAsync(sut.LogInCommand);

        Assert.True(sut.IsSignedIn);
        Assert.True(sut.ShowResume);
        Assert.False(sut.ShowLogIn);
    }

    [Fact]
    public async Task LogInCommand_ShouldStaySignedOut_WhenLoginCancelled()
    {
        SetupSession(SessionResult.Unauthenticated);
        var sut = CreateSut();
        await sut.LoadStateAsync();

        _login.Setup(l => l.LoginAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(InteractiveLoginOutcome.Cancelled);

        await RunAsync(sut.LogInCommand);

        Assert.False(sut.IsSignedIn);
        Assert.True(sut.ShowLogIn);
    }

    [Fact]
    public async Task LoadStateAsync_ShouldLoadPlayerName_WhenSignedIn()
    {
        SetupSession(SessionResult.NoGame);
        _accessToken.Setup(a => a.GetAccessTokenAsync(It.IsAny<CancellationToken>())).ReturnsAsync("tok");
        _userApi.Setup(u => u.GetCurrentUserAsync("tok", It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserSettingsResult.Success(new UserSettings("Ghost", "en")));
        var sut = CreateSut();

        await sut.LoadStateAsync();
        await Task.Delay(10); // The player-name load is fire-and-forget off LoadStateAsync.

        Assert.Equal("Ghost", sut.PlayerName);
        Assert.True(sut.ShowPlayerName);
    }

    [Fact]
    public async Task RefreshPlayerNameAsync_ShouldNotFetch_WhenSignedOut()
    {
        SetupSession(SessionResult.Unauthenticated);
        var sut = CreateSut();
        await sut.LoadStateAsync();

        await sut.RefreshPlayerNameAsync();

        Assert.Equal(string.Empty, sut.PlayerName);
        Assert.False(sut.ShowPlayerName);
        _userApi.Verify(u => u.GetCurrentUserAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task LogOutCommand_ShouldClearPlayerName()
    {
        SetupSession(SessionResult.NoGame);
        _accessToken.Setup(a => a.GetAccessTokenAsync(It.IsAny<CancellationToken>())).ReturnsAsync("tok");
        _userApi.Setup(u => u.GetCurrentUserAsync("tok", It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserSettingsResult.Success(new UserSettings("Ghost", "en")));
        var sut = CreateSut();
        await sut.LoadStateAsync();
        await Task.Delay(10);

        await RunAsync(sut.LogOutCommand);

        Assert.Equal(string.Empty, sut.PlayerName);
        Assert.False(sut.ShowPlayerName);
    }

    [Fact]
    public void ExitCommand_ShouldQuitApplication()
    {
        var sut = CreateSut();

        sut.ExitCommand.Execute(null);

        _app.Verify(a => a.Exit(), Times.Once);
    }

    [Fact]
    public void FieldManualVersion_ShouldEmbedAppVersion()
    {
        _version.SetupGet(v => v.Version).Returns("9.9");
        var sut = CreateSut();

        Assert.Equal("OPERATIONAL FIELD MANUAL — V 9.9", sut.FieldManualVersion);
    }

    [Fact]
    public void ShowExit_ShouldBeFalse_WhenPlatformDoesNotSupportExit()
    {
        _app.SetupGet(a => a.IsExitSupported).Returns(false);
        var sut = CreateSut();

        Assert.False(sut.ShowExit);
    }

    // RelayCommand.Execute is async void. With the mocked (already-completed) tasks the command
    // bodies run to completion synchronously; the small delay flushes any posted continuation
    // before assertions observe the settled state.
    private static async Task RunAsync(RelayCommand command)
    {
        command.Execute(null);
        await Task.Delay(10);
    }
}
