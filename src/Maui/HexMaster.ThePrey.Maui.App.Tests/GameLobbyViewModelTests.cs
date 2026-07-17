using HexMaster.ThePrey.Maui.App.Configuration;
using HexMaster.ThePrey.Maui.App.Services.Api;
using HexMaster.ThePrey.Maui.App.Services.Authentication;
using HexMaster.ThePrey.Maui.App.Services.Localization;
using HexMaster.ThePrey.Maui.App.Services.Navigation;
using HexMaster.ThePrey.Maui.App.Services.Platform;
using HexMaster.ThePrey.Maui.App.Services.Realtime;
using HexMaster.ThePrey.Maui.App.Services.Session;
using HexMaster.ThePrey.Maui.App.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace HexMaster.ThePrey.Maui.App.Tests;

public class GameLobbyViewModelTests
{
    private readonly Mock<IGameApiClient> _gameApi = new();
    private readonly Mock<IShareService> _share = new();
    private readonly Mock<ILobbyNavigator> _navigator = new();
    private readonly Mock<IAccessTokenProvider> _tokenProvider = new();
    private readonly Mock<ILocalizationService> _localization = new();
    private readonly Mock<ICurrentUserProvider> _currentUser = new();
    private readonly FakeGameStateService _gameState = new();

    public GameLobbyViewModelTests()
    {
        _tokenProvider.Setup(t => t.GetAccessTokenAsync(It.IsAny<CancellationToken>())).ReturnsAsync("token");
        _localization.Setup(l => l[It.IsAny<string>()]).Returns<string>(k => k);
        _currentUser.Setup(u => u.GetUserIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync((Guid?)null);
    }

    private GameLobbyViewModel CreateSut() => new(
        _gameApi.Object, _gameState, _currentUser.Object, _share.Object, _navigator.Object, _tokenProvider.Object,
        _localization.Object, Options.Create(new ThePreyClientOptions()),
        NullLogger<GameLobbyViewModel>.Instance);

    private static GameParticipantDetails Participant(Guid id, string name = "Alice", bool ready = false) =>
        new(id, name, ready, "Lobby");

    private static GameDetails Game(
        Guid? id = null,
        string code = "1234",
        string status = "Lobby",
        bool isOwner = true,
        bool isReadyToStart = false,
        Guid? hunter = null,
        int pingSeconds = 120,
        int endgamePingSeconds = 60,
        IReadOnlyList<GameParticipantDetails>? participants = null) =>
        new(
            id ?? Guid.NewGuid(),
            code,
            status,
            new GameConfigurationDetails(30, 5, 10, pingSeconds, endgamePingSeconds),
            participants ?? [Participant(Guid.NewGuid())],
            hunter,
            OwnerUserId: Guid.NewGuid(),
            IsOwnerPlayer: isOwner,
            IsReadyToStart: isReadyToStart);

    private void SetupLoad(GameDetails game)
    {
        var status = new GameStatus { GameId = game.Id };
        _gameApi.Setup(g => g.GetActiveGameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ActiveGameResult.Active(status));
        _gameApi.Setup(g => g.GetGameAsync(game.Id, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GetGameResult.Success(game));
    }

    // ---- Load ----

    [Fact]
    public async Task LoadAsync_ShouldResolveActiveThenFullGame_AndPopulate()
    {
        var game = Game(code: "9876", pingSeconds: 120, endgamePingSeconds: 60);
        SetupLoad(game);
        var sut = CreateSut();

        await sut.LoadAsync();

        Assert.True(sut.IsLoaded);
        Assert.False(sut.HasError);
        Assert.Equal("9876", sut.PassCode);
        // Ping seconds are shown in minutes.
        Assert.Equal(2, sut.SelectedPing);
        Assert.Equal(1, sut.SelectedEndgamePing);
    }

    [Fact]
    public async Task LoadAsync_ShouldLoadTargetGameById_WithoutQueryingActive_WhenTargetSet()
    {
        // A just-created game is opened by id: the lobby must load it directly and must NOT fall back to
        // GET /games/active (which only returns started games and would 404 for a fresh lobby).
        var game = Game(code: "5555");
        _gameApi.Setup(g => g.GetGameAsync(game.Id, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GetGameResult.Success(game));
        var sut = CreateSut();
        sut.SetTargetGame(game.Id);

        await sut.LoadAsync();

        Assert.True(sut.IsLoaded);
        Assert.False(sut.HasError);
        Assert.Equal("5555", sut.PassCode);
        _gameApi.Verify(g => g.GetActiveGameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _gameApi.Verify(g => g.GetGameAsync(game.Id, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task LoadAsync_ShouldError_WhenNoActiveGame()
    {
        _gameApi.Setup(g => g.GetActiveGameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ActiveGameResult.None);
        var sut = CreateSut();

        await sut.LoadAsync();

        Assert.True(sut.HasError);
        Assert.False(sut.IsLoaded);
    }

    [Fact]
    public async Task LoadAsync_ShouldError_WhenNoToken()
    {
        _tokenProvider.Setup(t => t.GetAccessTokenAsync(It.IsAny<CancellationToken>())).ReturnsAsync((string?)null);
        var sut = CreateSut();

        await sut.LoadAsync();

        Assert.True(sut.HasError);
    }

    [Fact]
    public async Task LoadAsync_ShouldError_WhenGetGameNotFound()
    {
        var id = Guid.NewGuid();
        _gameApi.Setup(g => g.GetActiveGameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ActiveGameResult.Active(new GameStatus { GameId = id }));
        _gameApi.Setup(g => g.GetGameAsync(id, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GetGameResult.NotFound);
        var sut = CreateSut();

        await sut.LoadAsync();

        Assert.True(sut.HasError);
    }

    [Fact]
    public async Task LoadAsync_ShouldInvalidateToken_WhenUnauthorized()
    {
        var id = Guid.NewGuid();
        _gameApi.Setup(g => g.GetActiveGameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ActiveGameResult.Active(new GameStatus { GameId = id }));
        _gameApi.Setup(g => g.GetGameAsync(id, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GetGameResult.Unauthorized);
        var sut = CreateSut();

        await sut.LoadAsync();

        Assert.True(sut.HasError);
        _tokenProvider.Verify(t => t.Invalidate(), Times.Once);
    }

    // ---- Owner gating ----

    [Fact]
    public async Task Load_ShouldEnableEditingAndStartForOwner_AndHideReady()
    {
        SetupLoad(Game(isOwner: true));
        var sut = CreateSut();

        await sut.LoadAsync();

        Assert.True(sut.SettingsEditable);
        Assert.True(sut.ShowStart);
        Assert.False(sut.ShowReady);
    }

    [Fact]
    public async Task Load_ShouldReadOnlyAndShowReadyForNonOwner_AndHideStart()
    {
        SetupLoad(Game(isOwner: false));
        var sut = CreateSut();

        await sut.LoadAsync();

        Assert.False(sut.SettingsEditable);
        Assert.False(sut.ShowStart);
        Assert.True(sut.ShowReady);
    }

    [Fact]
    public async Task Load_ShouldSuppressReadyBadgeForOwnerRow_ButKeepItForOtherPlayers()
    {
        // The game creator never has to ready up, so their row shows neither the READY nor the
        // NOT READY badge; every other participant still shows their ready state.
        var ownerId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        var game = new GameDetails(
            Guid.NewGuid(), "1234", "Lobby",
            new GameConfigurationDetails(30, 5, 10, 120, 60),
            [Participant(ownerId, "Owner", ready: false), Participant(otherId, "Bob", ready: true)],
            HunterUserId: null, OwnerUserId: ownerId, IsOwnerPlayer: true, IsReadyToStart: false);
        SetupLoad(game);
        var sut = CreateSut();

        await sut.LoadAsync();

        var owner = sut.Participants.Single(p => p.UserId == ownerId);
        Assert.True(owner.IsOwner);
        Assert.False(owner.ShowReady);
        Assert.False(owner.ShowNotReady);

        var other = sut.Participants.Single(p => p.UserId == otherId);
        Assert.False(other.IsOwner);
        Assert.True(other.ShowReady);
        Assert.False(other.ShowNotReady);
    }

    [Fact]
    public async Task DesignateHunter_ShouldBeInertForNonOwner()
    {
        var participant = new LobbyParticipant(Guid.NewGuid(), "Bob", false, false, false);
        SetupLoad(Game(isOwner: false));
        var sut = CreateSut();
        await sut.LoadAsync();

        await sut.DesignateHunterAsync(participant);

        _gameApi.Verify(g => g.DesignateHunterAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DesignateHunter_ShouldSendForOwner_AndApplySnapshot()
    {
        var game = Game(isOwner: true);
        SetupLoad(game);
        var target = game.Participants[0];
        var updated = game with { HunterUserId = target.UserId };
        _gameApi.Setup(g => g.DesignateHunterAsync(game.Id, target.UserId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(DesignateHunterResult.Success(updated));
        var sut = CreateSut();
        await sut.LoadAsync();

        await sut.DesignateHunterAsync(new LobbyParticipant(target, null, game.OwnerUserId));

        Assert.True(sut.Participants[0].IsHunter);
    }

    [Fact]
    public async Task DesignateHunter_ShouldNeverHandOff_EvenIfResponseCarriesStartedStatus()
    {
        // Regression: designating a hunter is a pure lobby action and must never navigate to gameplay —
        // the owner still has to press START. Even if the command response were to echo back a non-lobby
        // status, only a load (resume) or the live game-started stream frame may hand off, so the lobby
        // stays put here. (Repro: a sole owner tapping their own name jumped straight into the game.)
        var game = Game(isOwner: true);
        SetupLoad(game);
        var target = game.Participants[0];
        var echoedStarted = game with { HunterUserId = target.UserId, Status = "InProgress" };
        _gameApi.Setup(g => g.DesignateHunterAsync(game.Id, target.UserId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(DesignateHunterResult.Success(echoedStarted));
        var sut = CreateSut();
        await sut.LoadAsync();

        await sut.DesignateHunterAsync(new LobbyParticipant(target, null, game.OwnerUserId));

        _navigator.Verify(n => n.GoToGameplayAsync(), Times.Never);
        Assert.True(sut.Participants[0].IsHunter);
    }

    [Fact]
    public async Task SetReady_ShouldSendForNonOwner()
    {
        var game = Game(isOwner: false);
        SetupLoad(game);
        _gameApi.Setup(g => g.SetReadyAsync(game.Id, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SetReadyResult.Success(game));
        var sut = CreateSut();
        await sut.LoadAsync();

        await sut.SetReadyAsync();

        _gameApi.Verify(g => g.SetReadyAsync(game.Id, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SetReady_ShouldBeInertForOwner()
    {
        SetupLoad(Game(isOwner: true));
        var sut = CreateSut();
        await sut.LoadAsync();

        await sut.SetReadyAsync();

        _gameApi.Verify(g => g.SetReadyAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ---- Settings save ----

    [Fact]
    public async Task SaveSettings_ShouldSendMinutes_AndDisableStartWhenSnapshotResetsReadiness()
    {
        var game = Game(isOwner: true, isReadyToStart: true);
        SetupLoad(game);
        GameSettingsParameters? sent = null;
        var reset = game with { IsReadyToStart = false };
        _gameApi.Setup(g => g.UpdateGameSettingsAsync(
                game.Id, It.IsAny<GameSettingsParameters>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, GameSettingsParameters, string, CancellationToken>((_, p, _, _) => sent = p)
            .ReturnsAsync(UpdateGameSettingsResult.Success(reset));
        var sut = CreateSut();
        await sut.LoadAsync();
        Assert.True(sut.CanStart);

        await sut.SaveSettingsAsync();

        // The VM sends minute values; the ×60 conversion belongs to the client (see GameApiClientTests).
        Assert.Equal(sut.SelectedPing, sent!.PingMinutes);
        Assert.Equal(2, sent.PingMinutes);
        Assert.False(sut.CanStart);
    }

    [Fact]
    public async Task SaveSettings_ShouldSurfaceForbidden_AndKeepPage()
    {
        var game = Game(isOwner: true);
        SetupLoad(game);
        _gameApi.Setup(g => g.UpdateGameSettingsAsync(
                It.IsAny<Guid>(), It.IsAny<GameSettingsParameters>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UpdateGameSettingsResult.Forbidden);
        var sut = CreateSut();
        await sut.LoadAsync();

        await sut.SaveSettingsAsync();

        Assert.True(sut.ActionError);
        Assert.True(sut.IsLoaded);
    }

    // ---- Start ----

    [Fact]
    public async Task Start_CanStart_ShouldTrackIsReadyToStart()
    {
        SetupLoad(Game(isOwner: true, isReadyToStart: false));
        var sut = CreateSut();
        await sut.LoadAsync();
        Assert.False(sut.CanStart);

        SetupLoad(Game(isOwner: true, isReadyToStart: true, hunter: Guid.NewGuid()));
        await sut.LoadAsync();
        Assert.True(sut.CanStart);
    }

    [Fact]
    public async Task Start_ShouldHandOff_OnSuccess()
    {
        var hunter = Guid.NewGuid();
        var game = Game(isOwner: true, isReadyToStart: true, hunter: hunter);
        SetupLoad(game);
        _gameApi.Setup(g => g.StartGameAsync(game.Id, hunter, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(StartGameResult.Success(game with { Status = "InProgress" }));
        var sut = CreateSut();
        await sut.LoadAsync();

        await sut.StartAsync();

        _navigator.Verify(n => n.GoToGameplayAsync(), Times.Once);
    }

    [Fact]
    public async Task Start_ShouldSurfaceError_AndNotHandOff_OnRejection()
    {
        var hunter = Guid.NewGuid();
        var game = Game(isOwner: true, isReadyToStart: true, hunter: hunter);
        SetupLoad(game);
        _gameApi.Setup(g => g.StartGameAsync(game.Id, hunter, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(StartGameResult.Validation);
        var sut = CreateSut();
        await sut.LoadAsync();

        await sut.StartAsync();

        Assert.True(sut.ActionError);
        _navigator.Verify(n => n.GoToGameplayAsync(), Times.Never);
    }

    // ---- Live updates ----

    [Fact]
    public async Task LiveUpdate_ReadyChangeSnapshot_ShouldUpdateRowAndEnableStart()
    {
        var participant = Participant(Guid.NewGuid(), ready: false);
        var initial = Game(isOwner: true, isReadyToStart: false, participants: [participant]);
        var updated = initial with
        {
            IsReadyToStart = true,
            Participants = [participant with { IsReady = true }]
        };
        SetupLoad(initial);
        var sut = CreateSut();

        await sut.ActivateAsync();
        _gameState.Push(updated);

        Assert.True(sut.Participants[0].IsReady);
        Assert.True(sut.CanStart);
    }

    [Fact]
    public async Task LiveUpdate_OwnerSnapshotWithoutPersonalizedOwnerFlag_ShouldNotFlipOwner_NorLoseParticipants()
    {
        // Regression (the original report): the owner's lobby flipped to the participant view and the
        // roster vanished on the first live update. The Web PubSub group broadcast is one shared GameDto,
        // so its per-recipient IsOwnerPlayer arrives false. The owner must stay the owner (sticky) and the
        // participants must survive — ApplySnapshot never downgrades a known owner off a shared snapshot.
        var owner = Participant(Guid.NewGuid(), "Owner");
        var other = Participant(Guid.NewGuid(), "Bob", ready: true);
        var initial = Game(isOwner: true, participants: [owner, other]);          // load: personalized owner = true
        var liveShared = initial with { IsOwnerPlayer = false };                   // Web PubSub broadcast: false
        SetupLoad(initial);
        var sut = CreateSut();

        await sut.ActivateAsync();
        Assert.True(sut.IsOwner);

        _gameState.Push(liveShared);

        Assert.True(sut.IsOwner);            // did not flip to the participant view
        Assert.True(sut.ShowStart);
        Assert.False(sut.ShowReady);
        Assert.Equal(2, sut.Participants.Count); // roster preserved
    }

    [Fact]
    public async Task LiveUpdate_SharedSnapshot_ShouldDeriveOwnershipFromOwnerId_WhenSelfKnown()
    {
        // Even without a prior personalized load establishing ownership, the lobby derives ownership from
        // the owner id vs. the signed-in user — so a shared broadcast with IsOwnerPlayer=false still shows
        // the owner controls to the actual owner.
        var ownerId = Guid.NewGuid();
        _currentUser.Setup(u => u.GetUserIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(ownerId);
        var initial = new GameDetails(
            Guid.NewGuid(), "1234", "Lobby",
            new GameConfigurationDetails(30, 5, 10, 120, 60),
            [Participant(ownerId, "Owner")],
            HunterUserId: null, OwnerUserId: ownerId, IsOwnerPlayer: false, IsReadyToStart: false);
        SetupLoad(initial);
        var sut = CreateSut();

        await sut.ActivateAsync();
        _gameState.Push(initial);

        Assert.True(sut.IsOwner);
    }

    [Fact]
    public async Task LiveUpdate_StartedSnapshot_ShouldHandOff()
    {
        var initial = Game(isOwner: false);
        var started = initial with { Status = "InProgress" };
        SetupLoad(initial);
        var sut = CreateSut();

        await sut.ActivateAsync();
        _gameState.Push(started);

        _navigator.Verify(n => n.GoToGameplayAsync(), Times.Once);
    }

    [Fact]
    public async Task LiveUpdate_StartedStatusSnapshot_ShouldHandOff()
    {
        // The owner pressing START moves the game to Started (armed, awaiting the sweep) — this is a genuine
        // hand-off signal, exactly like InProgress.
        var initial = Game(isOwner: false);
        var started = initial with { Status = "Started" };
        SetupLoad(initial);
        var sut = CreateSut();

        await sut.ActivateAsync();
        _gameState.Push(started);

        _navigator.Verify(n => n.GoToGameplayAsync(), Times.Once);
    }

    [Fact]
    public async Task LiveUpdate_ReadySnapshot_ShouldNotHandOff()
    {
        // Ready means every non-owner readied up (the owner's START button is now enabled) — it must NOT
        // navigate anyone. Only the owner actually starting (→ Started) hands off.
        var initial = Game(isOwner: false);
        var ready = initial with { Status = "Ready", IsReadyToStart = true };
        SetupLoad(initial);
        var sut = CreateSut();

        await sut.ActivateAsync();
        _gameState.Push(ready);

        _navigator.Verify(n => n.GoToGameplayAsync(), Times.Never);
    }

    [Fact]
    public async Task LiveUpdate_PartialFrameWithoutConfigOrParticipants_ShouldNotThrow_KeepSeededValues_AndStillHandOff()
    {
        // A live snapshot can be partial: the backend's JSON may omit the configuration/participants
        // (e.g. a state-changed event that starts the game). These deserialize to null despite the
        // non-nullable record annotations, so ApplySnapshot must not dereference them — and, crucially, it
        // must still run the started→gameplay hand-off (regression: the NRE previously aborted before it).
        var initial = Game(isOwner: false, pingSeconds: 120, endgamePingSeconds: 60);
        var partialStart = initial with { Status = "InProgress", Configuration = null!, Participants = null! };
        SetupLoad(initial);
        var sut = CreateSut();

        await sut.ActivateAsync();
        _gameState.Push(partialStart);

        // Selectors keep the values seeded from the full initial load rather than being cleared/crashing.
        Assert.Equal(2, sut.SelectedPing);
        Assert.Equal(1, sut.SelectedEndgamePing);
        _navigator.Verify(n => n.GoToGameplayAsync(), Times.Once);
    }

    [Fact]
    public async Task Activate_ShouldStartSharedChannel_AndSubscribe()
    {
        var game = Game(isOwner: true);
        SetupLoad(game);
        var sut = CreateSut();

        await sut.ActivateAsync();

        Assert.Equal(game.Id, _gameState.StartedGameId);
        Assert.Equal(1, _gameState.SubscriberCount);
    }

    [Fact]
    public async Task Deactivate_ShouldUnsubscribe_AndStopChannel()
    {
        var game = Game(isOwner: true);
        SetupLoad(game);
        var sut = CreateSut();

        await sut.ActivateAsync();
        Assert.Equal(1, _gameState.SubscriberCount);

        sut.Deactivate();

        Assert.Equal(0, _gameState.SubscriberCount);
        Assert.True(_gameState.Stopped);
    }

    // ---- Share ----

    [Fact]
    public async Task Share_ShouldInvokeShareSheet_WithCodeAndLink()
    {
        var gameId = Guid.NewGuid();
        var game = Game(id: gameId, code: "1234");
        SetupLoad(game);
        _localization.Setup(l => l["Lobby_Invite_Template"]).Returns("code {0} link {1}");
        _localization.Setup(l => l["Lobby_Invite_Title"]).Returns("title");
        string? sharedText = null;
        _share.Setup(s => s.ShareTextAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string>((_, text) => sharedText = text)
            .Returns(Task.CompletedTask);
        var sut = CreateSut();
        await sut.LoadAsync();

        sut.ShareCommand.Execute(null);

        _share.Verify(s => s.ShareTextAsync("title", It.IsAny<string>()), Times.Once);
        // The pass code is shared as text; the link carries the game id (a Guid), which is what the
        // deep-link handler accepts — not the pass code.
        Assert.Contains("1234", sharedText);
        Assert.Contains($"https://theprey.nl/join/{gameId}", sharedText);
    }

}
