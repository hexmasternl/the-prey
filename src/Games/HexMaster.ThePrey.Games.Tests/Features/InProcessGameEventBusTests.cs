using HexMaster.ThePrey.Games.Notifications;

namespace HexMaster.ThePrey.Games.Tests.Features;

public sealed class InProcessGameEventBusTests
{
    private readonly InProcessGameEventBus _bus = new();

    [Fact]
    public async Task PublishAsync_ShouldDeliverEvent_ToSubscriber()
    {
        var gameId = Guid.NewGuid();
        var expected = new StateChangedEvent(gameId, "InProgress");

        var subscription = _bus.Subscribe(gameId);
        await _bus.PublishAsync(gameId, expected);
        _bus.Complete(gameId);

        var received = new List<GameEvent>();
        await foreach (var evt in subscription)
            received.Add(evt);

        Assert.Single(received);
        Assert.Equal(expected, received[0]);
    }

    [Fact]
    public async Task PublishAsync_ShouldNotDeliverEvent_ToDifferentGameSubscriber()
    {
        var gameA = Guid.NewGuid();
        var gameB = Guid.NewGuid();

        var subscriptionB = _bus.Subscribe(gameB);

        await _bus.PublishAsync(gameA, new StateChangedEvent(gameA, "InProgress"));
        _bus.Complete(gameA);
        _bus.Complete(gameB);

        var received = new List<GameEvent>();
        await foreach (var evt in subscriptionB)
            received.Add(evt);

        Assert.Empty(received);
    }

    [Fact]
    public async Task Complete_ShouldCloseSubscriberStream()
    {
        var gameId = Guid.NewGuid();
        var subscription = _bus.Subscribe(gameId);

        _bus.Complete(gameId);

        var count = 0;
        await foreach (var _ in subscription)
            count++;

        Assert.Equal(0, count);
    }

    [Fact]
    public async Task PublishAsync_ShouldDeliverMultipleEvents_InOrder()
    {
        var gameId = Guid.NewGuid();
        var subscription = _bus.Subscribe(gameId);

        await _bus.PublishAsync(gameId, new StateChangedEvent(gameId, "InProgress"));
        await _bus.PublishAsync(gameId, new ParticipantLocatedEvent(gameId, "Hunter", 52.0, 5.0));
        await _bus.PublishAsync(gameId, new GameEndedEvent(gameId));
        _bus.Complete(gameId);

        var received = new List<GameEvent>();
        await foreach (var evt in subscription)
            received.Add(evt);

        Assert.Equal(3, received.Count);
        Assert.IsType<StateChangedEvent>(received[0]);
        Assert.IsType<ParticipantLocatedEvent>(received[1]);
        Assert.IsType<GameEndedEvent>(received[2]);
    }
}
