using HexMaster.ThePrey.Maui.App.Services.Api;
using HexMaster.ThePrey.Maui.App.Services.Authentication;
using HexMaster.ThePrey.Maui.App.Services.Localization;
using HexMaster.ThePrey.Maui.App.Services.Navigation;
using HexMaster.ThePrey.Maui.App.Services.Session;
using HexMaster.ThePrey.Maui.App.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace HexMaster.ThePrey.Maui.App.Tests;

public class OutcomeViewModelTests
{
    private readonly Mock<IGameApiClient> _api = new();
    private readonly Mock<IAccessTokenProvider> _tokens = new();
    private readonly Mock<ICurrentUserProvider> _currentUser = new();
    private readonly Mock<IOutcomeNavigator> _navigator = new();
    private readonly Mock<ILocalizationService> _localization = new();
    private readonly Guid _gameId = Guid.NewGuid();
    private readonly Guid _hunterId = Guid.NewGuid();

    public OutcomeViewModelTests()
    {
        // Echo the key back, except the survivor formats — so the tests assert on keys, not on English copy.
        _localization.Setup(l => l[It.IsAny<string>()]).Returns((string key) => key switch
        {
            "Outcome_Survivors_One" => "{0} escaped",
            "Outcome_Survivors_Many" => "{0} escaped",
            _ => key
        });
        _tokens.Setup(t => t.GetAccessTokenAsync(It.IsAny<CancellationToken>())).ReturnsAsync("token");
    }

    private OutcomeViewModel CreateSut() => new(
        _api.Object, _tokens.Object, _currentUser.Object, _navigator.Object, _localization.Object,
        NullLogger<OutcomeViewModel>.Instance);

    [Fact]
    public async Task LoadAsync_ShouldShowVictory_WhenHunterCaughtEveryPrey()
    {
        ArrangeGame(Participant(_hunterId, "Active"), Participant(Guid.NewGuid(), "Tagged"));
        _currentUser.Setup(c => c.GetUserIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(_hunterId);

        var sut = CreateSut();
        sut.Initialize(_gameId, isHunter: true);
        await sut.LoadAsync();

        Assert.True(sut.IsResolved);
        Assert.True(sut.LocalPlayerWon);
        Assert.True(sut.ShowsVictory);
        Assert.False(sut.IsBusy);
        Assert.Equal("Outcome_Headline_Victory", sut.HeadlineText);
        Assert.Equal("Outcome_WinningSide_Hunter", sut.WinningSideText);
        Assert.Equal("Outcome_Reason_AllCaught", sut.ReasonText);
        Assert.False(sut.HasSurvivors);
    }

    [Fact]
    public async Task LoadAsync_ShouldShowDefeatWithSurvivorCount_WhenHunterRanOutOfTime()
    {
        ArrangeGame(
            Participant(_hunterId, "Active"),
            Participant(Guid.NewGuid(), "Active"),
            Participant(Guid.NewGuid(), "Active"),
            Participant(Guid.NewGuid(), "Tagged"));
        _currentUser.Setup(c => c.GetUserIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(_hunterId);

        var sut = CreateSut();
        sut.Initialize(_gameId, isHunter: true);
        await sut.LoadAsync();

        Assert.True(sut.LocalPlayerLost);
        Assert.False(sut.ShowsVictory);
        Assert.Equal("Outcome_Headline_Defeat", sut.HeadlineText);
        Assert.Equal("Outcome_WinningSide_Preys", sut.WinningSideText);
        Assert.Equal("Outcome_Reason_TimeExpired", sut.ReasonText);
        // The losing hunter is told how many got away.
        Assert.True(sut.HasSurvivors);
        Assert.Equal("2 escaped", sut.SurvivorsText);
    }

    [Fact]
    public async Task LoadAsync_ShouldShowVictory_WhenSurvivingPreyOutlastedTheClock()
    {
        var preyId = Guid.NewGuid();
        ArrangeGame(Participant(_hunterId, "Active"), Participant(preyId, "Active"));
        _currentUser.Setup(c => c.GetUserIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(preyId);

        var sut = CreateSut();
        sut.Initialize(_gameId, isHunter: false);
        await sut.LoadAsync();

        Assert.True(sut.LocalPlayerWon);
        Assert.Equal("Outcome_Headline_Victory", sut.HeadlineText);
        Assert.Equal("1 escaped", sut.SurvivorsText);
    }

    [Fact]
    public async Task LoadAsync_ShouldShowDefeat_WhenCaughtPreyDidNotShareTheTimeWin()
    {
        var caughtPreyId = Guid.NewGuid();
        ArrangeGame(
            Participant(_hunterId, "Active"),
            Participant(caughtPreyId, "Tagged"),
            Participant(Guid.NewGuid(), "Active"));
        _currentUser.Setup(c => c.GetUserIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(caughtPreyId);

        var sut = CreateSut();
        sut.Initialize(_gameId, isHunter: false);
        await sut.LoadAsync();

        Assert.True(sut.LocalPlayerLost);
        Assert.Equal("Outcome_Headline_Defeat", sut.HeadlineText);
        Assert.Equal("Outcome_WinningSide_Preys", sut.WinningSideText);
    }

    [Theory]
    [InlineData(GetGameOutcome.NotFound)]
    [InlineData(GetGameOutcome.Error)]
    [InlineData(GetGameOutcome.Unauthorized)]
    public async Task LoadAsync_ShouldFallBackToTheNeutralState_WhenTheRecordCannotBeRead(GetGameOutcome outcome)
    {
        _api.Setup(a => a.GetGameAsync(_gameId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetGameResult(outcome, null));

        var sut = CreateSut();
        sut.Initialize(_gameId, isHunter: false);
        await sut.LoadAsync();

        Assert.False(sut.IsResolved);
        Assert.True(sut.ShowsNeutral);
        Assert.False(sut.ShowsVictory);
        Assert.False(sut.LocalPlayerLost);
        Assert.Equal("Outcome_Headline_Neutral", sut.HeadlineText);
        Assert.Equal(string.Empty, sut.WinningSideText);
        Assert.False(sut.IsBusy);

        // The player is never trapped: close still works from the neutral state.
        Assert.True(sut.CloseCommand.CanExecute(null));
    }

    [Fact]
    public async Task LoadAsync_ShouldFallBackToTheNeutralState_WhenNoTokenIsAvailable()
    {
        _tokens.Setup(t => t.GetAccessTokenAsync(It.IsAny<CancellationToken>())).ReturnsAsync((string?)null);

        var sut = CreateSut();
        sut.Initialize(_gameId, isHunter: false);
        await sut.LoadAsync();

        Assert.True(sut.ShowsNeutral);
        _api.Verify(
            a => a.GetGameAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task LoadAsync_ShouldReadTheRecordOnce_WhenCalledRepeatedly()
    {
        ArrangeGame(Participant(_hunterId, "Active"), Participant(Guid.NewGuid(), "Tagged"));
        _currentUser.Setup(c => c.GetUserIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(_hunterId);

        var sut = CreateSut();
        sut.Initialize(_gameId, isHunter: true);
        await sut.LoadAsync();
        await sut.LoadAsync();

        _api.Verify(
            a => a.GetGameAsync(_gameId, It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public void CloseCommand_ShouldReturnToTheMenu()
    {
        var sut = CreateSut();

        sut.CloseCommand.Execute(null);

        _navigator.Verify(n => n.ReturnToMenuAsync(), Times.Once);
    }

    private void ArrangeGame(params GameParticipantDetails[] participants) =>
        _api.Setup(a => a.GetGameAsync(_gameId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GetGameResult.Success(new GameDetails(
                _gameId, "1234", "Completed",
                new GameConfigurationDetails(30, 5, 10, 120, 60),
                participants,
                _hunterId, OwnerUserId: Guid.NewGuid(), IsOwnerPlayer: false, IsReadyToStart: false)));

    private static GameParticipantDetails Participant(Guid userId, string state) =>
        new(userId, "Callsign", IsReady: true, State: state);
}
