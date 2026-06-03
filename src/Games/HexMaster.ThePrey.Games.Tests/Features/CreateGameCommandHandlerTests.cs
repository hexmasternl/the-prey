using HexMaster.ThePrey.Games.DomainModels;
using HexMaster.ThePrey.Games.Features.CreateGame;
using HexMaster.ThePrey.Games.Observability;
using Microsoft.Extensions.Logging;
using Moq;

namespace HexMaster.ThePrey.Games.Tests.Features;

public sealed class CreateGameCommandHandlerTests
{
    private readonly Mock<IGameRepository> _repository = new();
    private readonly Mock<IGameMetrics> _metrics = new();
    private readonly CreateGameCommandHandler _handler;

    public CreateGameCommandHandlerTests()
    {
        _handler = new CreateGameCommandHandler(_repository.Object, _metrics.Object, Mock.Of<ILogger<CreateGameCommandHandler>>());
    }

    private static CreateGameCommand ValidCommand() =>
        new(Guid.NewGuid(), Guid.NewGuid(), 60, 5, 10, 30, 10, false, false);

    [Fact]
    public async Task Handle_ShouldCreateAndPersist_WhenCommandIsValid()
    {
        var command = ValidCommand();

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.NotEqual(Guid.Empty, result.Game.Id);
        Assert.Equal(command.OwnerUserId, result.Game.OwnerUserId);
        Assert.Equal(command.PlayfieldId, result.Game.PlayfieldId);
        Assert.Equal(GameStatus.Lobby.ToString(), result.Game.Status);
        _repository.Verify(r => r.AddAsync(It.IsAny<Game>(), It.IsAny<CancellationToken>()), Times.Once);
        _metrics.Verify(m => m.RecordGameCreated(), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldThrowAndNotPersist_WhenConfigurationIsInvalid()
    {
        // FinalStageDuration not shorter than GameDuration.
        var command = new CreateGameCommand(Guid.NewGuid(), Guid.NewGuid(), 60, 5, 60, 30, 10, false, false);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => _handler.Handle(command, CancellationToken.None));
        _repository.Verify(r => r.AddAsync(It.IsAny<Game>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenCommandIsNull()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _handler.Handle(null!, CancellationToken.None));
    }
}
