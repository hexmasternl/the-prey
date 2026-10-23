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

    private static CreateGameCommand ValidCommand(string displayName = "Owner") =>
        new(Guid.NewGuid(), Guid.NewGuid(), displayName, null, 60, 5, 10, 30, 10, false, false);

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
    public async Task Handle_ShouldAssignFourDigitGameCode_WhenCommandIsValid()
    {
        var result = await _handler.Handle(ValidCommand(), CancellationToken.None);

        Assert.Equal(Game.GameCodeLength, result.Game.GameCode.Length);
        Assert.All(result.Game.GameCode, c => Assert.True(char.IsAsciiDigit(c)));
    }

    [Fact]
    public async Task Handle_ShouldAddCreatorToLobby_WhenCommandIsValid()
    {
        var command = ValidCommand(displayName: "The Creator");

        var result = await _handler.Handle(command, CancellationToken.None);

        var player = Assert.Single(result.Game.Participants);
        Assert.Equal(command.OwnerUserId, player.UserId);
        Assert.Equal("The Creator", player.DisplayName);
    }

    [Fact]
    public async Task Handle_ShouldRetryWithFreshCode_WhenGameCodeCollides()
    {
        var seenCodes = new List<string>();
        _repository
            .Setup(r => r.AddAsync(It.IsAny<Game>(), It.IsAny<CancellationToken>()))
            .Callback<Game, CancellationToken>((game, _) => seenCodes.Add(game.GameCode))
            .Returns<Game, CancellationToken>((game, _) =>
                seenCodes.Count < 3 ? throw new DuplicateGameCodeException(game.GameCode) : Task.CompletedTask);

        var result = await _handler.Handle(ValidCommand(), CancellationToken.None);

        Assert.Equal(3, seenCodes.Count);
        Assert.Equal(seenCodes[^1], result.Game.GameCode);
        _repository.Verify(r => r.AddAsync(It.IsAny<Game>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
        _metrics.Verify(m => m.RecordGameCreated(), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenGameCodeKeepsColliding()
    {
        _repository
            .Setup(r => r.AddAsync(It.IsAny<Game>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DuplicateGameCodeException("0000"));

        await Assert.ThrowsAsync<DuplicateGameCodeException>(() => _handler.Handle(ValidCommand(), CancellationToken.None));

        _repository.Verify(
            r => r.AddAsync(It.IsAny<Game>(), It.IsAny<CancellationToken>()),
            Times.Exactly(CreateGameCommandHandler.MaxGameCodeAttempts));
        _metrics.Verify(m => m.RecordGameCreated(), Times.Never);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_ShouldThrowAndNotPersist_WhenDisplayNameIsEmpty(string displayName)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _handler.Handle(ValidCommand(displayName), CancellationToken.None));
        _repository.Verify(r => r.AddAsync(It.IsAny<Game>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldThrowAndNotPersist_WhenConfigurationIsInvalid()
    {
        // FinalStageDuration not shorter than GameDuration.
        var command = new CreateGameCommand(Guid.NewGuid(), Guid.NewGuid(), "Owner", null, 60, 5, 60, 30, 10, false, false);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => _handler.Handle(command, CancellationToken.None));
        _repository.Verify(r => r.AddAsync(It.IsAny<Game>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenCommandIsNull()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _handler.Handle(null!, CancellationToken.None));
    }
}
