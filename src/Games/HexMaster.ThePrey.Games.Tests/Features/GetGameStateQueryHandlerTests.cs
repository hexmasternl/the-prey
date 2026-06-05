using HexMaster.ThePrey.Games.DomainModels;
using HexMaster.ThePrey.Games.Features.GetGameState;
using HexMaster.ThePrey.Games.Tests.Factories;
using Moq;

namespace HexMaster.ThePrey.Games.Tests.Features;

public sealed class GetGameStateQueryHandlerTests
{
    private static readonly DateTimeOffset Start = new(2026, 6, 3, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Now = Start.AddMinutes(10);

    private readonly Mock<IGameRepository> _repository = new();
    private readonly GetGameStateQueryHandler _handler;

    public GetGameStateQueryHandlerTests()
    {
        _handler = new GetGameStateQueryHandler(_repository.Object);
    }

    [Fact]
    public async Task Handle_ShouldReturnHunterDistance_WhenPlayerIsPrey()
    {
        var game = GameFaker.StartedGame(out _, out var preyIds, Start);
        // Location is set by the engine broadcast cycle via UpdateBroadcastLocation, not RecordLocation
        game.Hunter!.UpdateBroadcastLocation(GpsCoordinate.Create(52.0, 5.0));
        game.Preys.Single(p => p.UserId == preyIds[0]).UpdateBroadcastLocation(GpsCoordinate.Create(52.001, 5.0));
        _repository.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);

        var state = await _handler.Handle(new GetGameStateQuery(game.Id, preyIds[0]), CancellationToken.None);

        Assert.NotNull(state);
        // 0.001° of latitude is ~111 meters.
        Assert.InRange(state!.HunterDistanceMeters!.Value, 105, 118);
        Assert.Empty(state.PreyLocations);
    }

    [Fact]
    public async Task Handle_ShouldReturnNullDistance_WhenHunterHasNoLocation()
    {
        var game = GameFaker.StartedGame(out _, out var preyIds, Start);
        game.Preys.Single(p => p.UserId == preyIds[0]).UpdateBroadcastLocation(GpsCoordinate.Create(52.001, 5.0));
        _repository.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);

        var state = await _handler.Handle(new GetGameStateQuery(game.Id, preyIds[0]), CancellationToken.None);

        Assert.NotNull(state);
        Assert.Null(state!.HunterDistanceMeters);
        Assert.Empty(state.PreyLocations);
    }

    [Fact]
    public async Task Handle_ShouldReturnPreyLocations_WhenPlayerIsHunter()
    {
        var game = GameFaker.StartedGame(out var hunterId, out var preyIds, Start, playerCount: 4);
        game.Preys.Single(p => p.UserId == preyIds[0]).UpdateBroadcastLocation(GpsCoordinate.Create(52.1, 5.1));
        game.Preys.Single(p => p.UserId == preyIds[1]).UpdateBroadcastLocation(GpsCoordinate.Create(52.2, 5.2));
        _repository.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);

        var state = await _handler.Handle(new GetGameStateQuery(game.Id, hunterId), CancellationToken.None);

        Assert.NotNull(state);
        Assert.Null(state!.HunterDistanceMeters);
        // The third prey has no broadcasted location yet and is excluded.
        Assert.Equal(2, state.PreyLocations.Count);
        Assert.Contains(state.PreyLocations, c => c.Latitude == 52.1 && c.Longitude == 5.1);
        Assert.Contains(state.PreyLocations, c => c.Latitude == 52.2 && c.Longitude == 5.2);
    }

    [Fact]
    public async Task Handle_ShouldReturnNull_WhenGameNotFound()
    {
        _repository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Game?)null);

        var state = await _handler.Handle(new GetGameStateQuery(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        Assert.Null(state);
    }

    [Fact]
    public async Task Handle_ShouldReturnNull_WhenGameHasEnded()
    {
        var game = GameFaker.StartedGame(out _, out var preyIds, Start);
        game.Complete(Now);
        _repository.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);

        var state = await _handler.Handle(new GetGameStateQuery(game.Id, preyIds[0]), CancellationToken.None);

        Assert.Null(state);
    }

    [Fact]
    public async Task Handle_ShouldReturnNull_WhenUserIsNotAParticipant()
    {
        var game = GameFaker.StartedGame(out _, out _, Start);
        _repository.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);

        var state = await _handler.Handle(new GetGameStateQuery(game.Id, Guid.NewGuid()), CancellationToken.None);

        Assert.Null(state);
    }
}
