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
    public async Task PublishAsync_ShouldDeliverEvent_ToAllSubscribers()
    {
        // Regression: a single shared channel made subscribers compete for events, so in a game
        // with multiple participants each event reached only one of them.
        var gameId = Guid.NewGuid();
        var expected = new GameEndedEvent(gameId, "PreysWin", 1);

        var subscriptionA = _bus.Subscribe(gameId);
        var subscriptionB = _bus.Subscribe(gameId);

        await _bus.PublishAsync(gameId, expected);
        _bus.Complete(gameId);

        var receivedA = new List<GameEvent>();
        await foreach (var evt in subscriptionA)
            receivedA.Add(evt);

        var receivedB = new List<GameEvent>();
        await foreach (var evt in subscriptionB)
            receivedB.Add(evt);

        Assert.Single(receivedA);
        Assert.Single(receivedB);
        Assert.Equal(expected, receivedA[0]);
        Assert.Equal(expected, receivedB[0]);
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
        await _bus.PublishAsync(gameId, new ParticipantLocatedEvent(gameId, Guid.NewGuid(), "Hunter", 52.0, 5.0, "Active"));
        await _bus.PublishAsync(gameId, new GameEndedEvent(gameId, "PreysWin", 1));
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
