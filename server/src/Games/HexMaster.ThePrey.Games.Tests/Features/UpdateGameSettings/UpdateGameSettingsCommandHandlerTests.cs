using HexMaster.ThePrey.Games.Abstractions.DataTransferObjects;
using HexMaster.ThePrey.Games.DomainModels;
using HexMaster.ThePrey.Games.Features.UpdateGameSettings;
using HexMaster.ThePrey.Games.Notifications;
using HexMaster.ThePrey.Games.Tests.Factories;
using HexMaster.ThePrey.IntegrationEvents;
using Moq;

namespace HexMaster.ThePrey.Games.Tests.Features.UpdateGameSettings;

public sealed class UpdateGameSettingsCommandHandlerTests
{
    private readonly Mock<IGameRepository> _repository = new();
    private readonly Mock<ILobbyEventBus> _eventBus = new();
    private readonly List<(string Type, object Payload)> _published = [];
    private readonly UpdateGameSettingsCommandHandler _handler;

    public UpdateGameSettingsCommandHandlerTests()
    {
        _eventBus.Setup(b => b.PublishAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, string, object, CancellationToken>((_, type, payload, _) => _published.Add((type, payload)))
            .Returns(ValueTask.CompletedTask);
        _handler = new UpdateGameSettingsCommandHandler(_repository.Object, _eventBus.Object);
    }

    private static UpdateGameSettingsCommand CommandFor(Game game, Guid? caller = null) => new(
        game.Id,
        caller ?? game.OwnerUserId,
        GameDuration: 60,
        HunterDelayTime: 5,
        FinalStageDuration: 10,
        DefaultLocationInterval: 180,
        FinalLocationInterval: 60,
        EnablePreyBoundaryPenalties: false,
        EnableHunterBoundaryPenalty: false);

    private List<ParticipantDto> ParticipantChanges() => _published
        .Where(m => m.Type == RealtimeProtocol.MessageTypes.ParticipantChanged)
        .Select(m => Assert.IsType<ParticipantDto>(m.Payload))
        .ToList();

    [Fact]
    public async Task Handle_ShouldPublishParticipantChangedPerResetPlayer_WhenReadyPlayersAreReset()
    {
        var game = GameFaker.LobbyGameWithPlayers(2, out var readyIds);
        var waiting = Guid.NewGuid();
        game.JoinLobby(GameFaker.Player(waiting));
        _repository.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);

        await _handler.Handle(CommandFor(game), CancellationToken.None);

        Assert.Single(_published, m => m.Type == RealtimeProtocol.MessageTypes.ConfigurationChanged);
        var changes = ParticipantChanges();
        Assert.Equal(2, changes.Count);
        Assert.Equal(readyIds.OrderBy(id => id), changes.Select(c => c.UserId).OrderBy(id => id));
        Assert.All(changes, c => Assert.False(c.IsReady));
    }

    [Fact]
    public async Task Handle_ShouldPublishConfigurationChangedFirst_WhenPlayersAreReset()
    {
        var game = GameFaker.LobbyGameWithPlayers(1, out _);
        _repository.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);

        await _handler.Handle(CommandFor(game), CancellationToken.None);

        Assert.Equal(RealtimeProtocol.MessageTypes.ConfigurationChanged, _published[0].Type);
        Assert.Equal(RealtimeProtocol.MessageTypes.ParticipantChanged, _published[1].Type);
    }

    [Fact]
    public async Task Handle_ShouldPublishNoParticipantChanged_WhenNoPlayerWasReady()
    {
        var game = GameFaker.LobbyGameWithPlayers(2, out _, markReady: false);
        _repository.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);

        await _handler.Handle(CommandFor(game), CancellationToken.None);

        Assert.Single(_published);
        Assert.Empty(ParticipantChanges());
    }

    [Fact]
    public async Task Handle_ShouldPublishNothing_WhenCallerIsNotTheOwner()
    {
        var game = GameFaker.LobbyGameWithPlayers(2, out var ids);
        _repository.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _handler.Handle(CommandFor(game, caller: ids[0]), CancellationToken.None));

        Assert.Empty(_published);
    }

    [Fact]
    public async Task Handle_ShouldReturnNull_WhenGameNotFound()
    {
        _repository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Game?)null);
        var game = GameFaker.LobbyGame();

        var result = await _handler.Handle(CommandFor(game), CancellationToken.None);

        Assert.Null(result);
        Assert.Empty(_published);
    }
}
