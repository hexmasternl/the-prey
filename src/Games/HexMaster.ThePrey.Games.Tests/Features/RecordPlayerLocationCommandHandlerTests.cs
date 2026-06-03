using HexMaster.ThePrey.Games.DomainModels;
using HexMaster.ThePrey.Games.Features.RecordPlayerLocation;
using HexMaster.ThePrey.Games.Observability;
using HexMaster.ThePrey.Games.Tests.Factories;
using Moq;

namespace HexMaster.ThePrey.Games.Tests.Features;

public sealed class RecordPlayerLocationCommandHandlerTests
{
    private static readonly DateTimeOffset Start = new(2026, 6, 3, 12, 0, 0, TimeSpan.Zero);
    // 10 minutes in: before the final stage, so the default interval (30s) applies.
    private static readonly DateTimeOffset Now = Start.AddMinutes(10);

    private readonly Mock<IGameRepository> _repository = new();
    private readonly Mock<IGameMetrics> _metrics = new();
    private readonly RecordPlayerLocationCommandHandler _handler;

    public RecordPlayerLocationCommandHandlerTests()
    {
        _handler = new RecordPlayerLocationCommandHandler(_repository.Object, _metrics.Object, new FixedTimeProvider(Now));
    }

    [Fact]
    public async Task Handle_ShouldRecordAndReturnNextInterval_WhenParticipantSubmits()
    {
        var game = GameFaker.StartedGame(out _, out var preyIds, Start, configuration: GameFaker.ValidConfiguration());
        _repository.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);

        var result = await _handler.Handle(
            new RecordPlayerLocationCommand(game.Id, preyIds[0], 52.1, 5.1, null), CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(result!.Response.Accepted);
        Assert.Equal(30, result.Response.NextLocationIntervalSeconds);
        _repository.Verify(r => r.UpdateAsync(game, It.IsAny<CancellationToken>()), Times.Once);
        _metrics.Verify(m => m.RecordLocationRecorded(), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenUserIsNotAParticipant()
    {
        var game = GameFaker.StartedGame(out _, out _, Start);
        _repository.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _handler.Handle(new RecordPlayerLocationCommand(game.Id, Guid.NewGuid(), 52.1, 5.1, null), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ShouldReturnNull_WhenGameNotFound()
    {
        _repository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Game?)null);

        var result = await _handler.Handle(
            new RecordPlayerLocationCommand(Guid.NewGuid(), Guid.NewGuid(), 52.1, 5.1, null), CancellationToken.None);

        Assert.Null(result);
    }
}
