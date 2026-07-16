using HexMaster.ThePrey.Maui.App.Services.Api;
using HexMaster.ThePrey.Maui.App.Services.Authentication;
using HexMaster.ThePrey.Maui.App.Services.Localization;
using HexMaster.ThePrey.Maui.App.Services.Navigation;
using HexMaster.ThePrey.Maui.App.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace HexMaster.ThePrey.Maui.App.Tests;

public class StartGameViewModelTests
{
    private const string DefaultDisplayName = "Player";

    private readonly Mock<IGameApiClient> _gameApi = new();
    private readonly Mock<IUserApiClient> _userApi = new();
    private readonly Mock<IAccessTokenProvider> _tokenProvider = new();
    private readonly Mock<IPlayfieldSelectNavigator> _picker = new();
    private readonly Mock<IMenuNavigator> _navigator = new();
    private readonly Mock<ILocalizationService> _localization = new();

    public StartGameViewModelTests()
    {
        _tokenProvider.Setup(t => t.GetAccessTokenAsync(It.IsAny<CancellationToken>())).ReturnsAsync("token");
        _userApi.Setup(u => u.GetCurrentUserAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserSettingsResult.Success(new UserSettings("Alice", "en")));
        _gameApi.Setup(g => g.CreateGameAsync(It.IsAny<CreateGameParameters>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateGameResult.Success(new GameSummary(Guid.NewGuid())));
        _navigator.Setup(n => n.GoToAsync(It.IsAny<string>())).Returns(Task.CompletedTask);
        _localization.Setup(l => l[StartGameViewModel.DefaultDisplayNameKey]).Returns(DefaultDisplayName);
    }

    private StartGameViewModel CreateSut() => new(
        _gameApi.Object, _userApi.Object, _tokenProvider.Object, _picker.Object, _navigator.Object,
        _localization.Object, NullLogger<StartGameViewModel>.Instance);

    private static PlayFieldSummary Pf(string name = "Harbour") => new(Guid.NewGuid(), name, false);

    private void SetupPickerReturns(PlayFieldSummary? result) =>
        _picker.Setup(p => p.SelectPlayfieldAsync(It.IsAny<CancellationToken>())).ReturnsAsync(result);

    private async Task<StartGameViewModel> WithSelectedPlayfield(PlayFieldSummary? pf = null)
    {
        var sut = CreateSut();
        SetupPickerReturns(pf ?? Pf());
        await sut.SelectPlayfieldAsync();
        return sut;
    }

    // ---- Defaults ----

    [Fact]
    public void Defaults_ShouldMatchTheProposal()
    {
        var sut = CreateSut();

        Assert.Equal(30, sut.SelectedDuration);
        Assert.Equal(5, sut.SelectedHeadstart);
        Assert.Equal(10, sut.SelectedEndgame);
        Assert.Equal(2, sut.SelectedPing);
        Assert.Equal(1, sut.SelectedEndgamePing);
    }

    [Fact]
    public void OptionLists_ShouldOfferOnlyTheListedChoices()
    {
        Assert.Equal([30, 60, 90], StartGameViewModel.DurationOptions);
        Assert.Equal([5, 10, 15], StartGameViewModel.HeadstartOptions);
        Assert.Equal([5, 10, 15], StartGameViewModel.EndgameOptions);
        Assert.Equal([2, 3, 5], StartGameViewModel.PingOptions);
        Assert.Equal([1, 2, 3, 5], StartGameViewModel.EndgamePingOptions);
    }

    // ---- CanCreate ----

    [Fact]
    public void CanCreate_ShouldBeFalse_WhenNoPlayfieldSelected()
    {
        var sut = CreateSut();

        Assert.False(sut.CanCreate);
        Assert.False(sut.HasSelectedPlayfield);
        Assert.True(sut.HasNoSelectedPlayfield);
    }

    [Fact]
    public async Task CanCreate_ShouldBeTrue_WhenPlayfieldSelected()
    {
        var sut = await WithSelectedPlayfield(Pf("Docks"));

        Assert.True(sut.CanCreate);
        Assert.True(sut.HasSelectedPlayfield);
        Assert.Equal("Docks", sut.SelectedPlayfieldName);
    }

    // ---- Select playfield ----

    [Fact]
    public async Task SelectPlayfield_ShouldStoreReturnedField()
    {
        var pf = Pf("Harbour");
        var sut = CreateSut();
        SetupPickerReturns(pf);

        await sut.SelectPlayfieldAsync();

        Assert.Same(pf, sut.SelectedPlayfield);
    }

    [Fact]
    public async Task SelectPlayfield_ShouldLeaveSelectionUnchanged_OnDismiss()
    {
        var first = Pf("Harbour");
        var sut = await WithSelectedPlayfield(first);

        // A second open that is dismissed (null) must not clear the previous selection.
        SetupPickerReturns(null);
        await sut.SelectPlayfieldAsync();

        Assert.Same(first, sut.SelectedPlayfield);
    }

    // ---- Create ----

    [Fact]
    public async Task Create_ShouldConvertPingMinutesToSeconds_AndKeepDurationsInMinutes()
    {
        var sut = await WithSelectedPlayfield();
        sut.SelectedDuration = 90;
        sut.SelectedPing = 2;         // → 120s
        sut.SelectedEndgamePing = 1;  // → 60s
        CreateGameParameters? captured = null;
        _gameApi.Setup(g => g.CreateGameAsync(It.IsAny<CreateGameParameters>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<CreateGameParameters, string, CancellationToken>((p, _, _) => captured = p)
            .ReturnsAsync(CreateGameResult.Success(new GameSummary(Guid.NewGuid())));

        await sut.CreateAsync();

        Assert.NotNull(captured);
        Assert.Equal(120, captured!.DefaultLocationIntervalSeconds);
        Assert.Equal(60, captured.FinalLocationIntervalSeconds);
        Assert.Equal(90, captured.GameDurationMinutes);
    }

    [Fact]
    public async Task Create_ShouldReplaceCreatePageWithLobby_PassingCreatedGameId_OnSuccess()
    {
        var gameId = Guid.NewGuid();
        var sut = await WithSelectedPlayfield();
        _gameApi.Setup(g => g.CreateGameAsync(It.IsAny<CreateGameParameters>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateGameResult.Success(new GameSummary(gameId)));

        await sut.CreateAsync();

        // Replaces the create page with the lobby ("../" pops this page first) and hands it the new game's
        // id so the lobby loads that game directly rather than resolving the (not-yet-started) active game.
        _navigator.Verify(n => n.GoToAsync(
            $"../{MainMenuViewModel.GameRoute}?{GameLobbyViewModel.GameIdQueryKey}={gameId}"), Times.Once);
        Assert.False(sut.HasError);
        Assert.False(sut.HasValidationError);
    }

    [Fact]
    public async Task Create_ShouldUseProfileDisplayName()
    {
        var sut = await WithSelectedPlayfield();
        CreateGameParameters? captured = null;
        _gameApi.Setup(g => g.CreateGameAsync(It.IsAny<CreateGameParameters>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<CreateGameParameters, string, CancellationToken>((p, _, _) => captured = p)
            .ReturnsAsync(CreateGameResult.Success(new GameSummary(Guid.NewGuid())));

        await sut.CreateAsync();

        Assert.Equal("Alice", captured!.DisplayName);
    }

    [Fact]
    public async Task Create_ShouldFallBackToDefaultDisplayName_WhenProfileNotFound()
    {
        var sut = await WithSelectedPlayfield();
        _userApi.Setup(u => u.GetCurrentUserAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserSettingsResult.NotFound);
        CreateGameParameters? captured = null;
        _gameApi.Setup(g => g.CreateGameAsync(It.IsAny<CreateGameParameters>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<CreateGameParameters, string, CancellationToken>((p, _, _) => captured = p)
            .ReturnsAsync(CreateGameResult.Success(new GameSummary(Guid.NewGuid())));

        await sut.CreateAsync();

        Assert.Equal(DefaultDisplayName, captured!.DisplayName);
    }

    [Fact]
    public async Task Create_ShouldError_WhenNoAccessToken()
    {
        var sut = await WithSelectedPlayfield();
        _tokenProvider.Setup(t => t.GetAccessTokenAsync(It.IsAny<CancellationToken>())).ReturnsAsync((string?)null);

        await sut.CreateAsync();

        Assert.True(sut.HasError);
        _gameApi.Verify(g => g.CreateGameAsync(It.IsAny<CreateGameParameters>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _navigator.Verify(n => n.GoToAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Create_ShouldShowValidationError_AndKeepSelections_OnValidation()
    {
        var pf = Pf("Harbour");
        var sut = await WithSelectedPlayfield(pf);
        _gameApi.Setup(g => g.CreateGameAsync(It.IsAny<CreateGameParameters>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateGameResult.Validation);

        await sut.CreateAsync();

        Assert.True(sut.HasValidationError);
        Assert.False(sut.HasError);
        Assert.Same(pf, sut.SelectedPlayfield);
        _navigator.Verify(n => n.GoToAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Create_ShouldInvalidateToken_AndError_OnUnauthorizedCreate()
    {
        var sut = await WithSelectedPlayfield();
        _gameApi.Setup(g => g.CreateGameAsync(It.IsAny<CreateGameParameters>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateGameResult.Unauthorized);

        await sut.CreateAsync();

        _tokenProvider.Verify(t => t.Invalidate(), Times.Once);
        Assert.True(sut.HasError);
    }

    [Fact]
    public async Task Create_ShouldInvalidateToken_AndError_WhenProfileReadUnauthorized()
    {
        var sut = await WithSelectedPlayfield();
        _userApi.Setup(u => u.GetCurrentUserAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserSettingsResult.Unauthorized);

        await sut.CreateAsync();

        _tokenProvider.Verify(t => t.Invalidate(), Times.Once);
        Assert.True(sut.HasError);
        _gameApi.Verify(g => g.CreateGameAsync(It.IsAny<CreateGameParameters>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Create_ShouldError_OnCreateError()
    {
        var sut = await WithSelectedPlayfield();
        _gameApi.Setup(g => g.CreateGameAsync(It.IsAny<CreateGameParameters>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateGameResult.Error);

        await sut.CreateAsync();

        Assert.True(sut.HasError);
        _navigator.Verify(n => n.GoToAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Create_ShouldDoNothing_WhenNoPlayfieldSelected()
    {
        var sut = CreateSut();

        await sut.CreateAsync();

        _gameApi.Verify(g => g.CreateGameAsync(It.IsAny<CreateGameParameters>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
