using HexMaster.ThePrey.Games.Abstractions.DataTransferObjects;
using HexMaster.ThePrey.Games.Notifications;

namespace HexMaster.ThePrey.Games.Tests.Features;

public sealed class InProcessLobbyEventBusTests
{
    private readonly InProcessLobbyEventBus _bus = new();

    [Fact]
    public async Task PublishAsync_ShouldDeliverEvent_ToAllSubscribers()
    {
        var gameId = Guid.NewGuid();
        var payload = CreatePayload(gameId);

        var subscriptionA = _bus.Subscribe(gameId);
        var subscriptionB = _bus.Subscribe(gameId);

        await _bus.PublishAsync(gameId, "lobby-updated", payload);
        _bus.Complete(gameId);

        var receivedA = new List<LobbyEvent>();
        await foreach (var evt in subscriptionA)
            receivedA.Add(evt);

        var receivedB = new List<LobbyEvent>();
        await foreach (var evt in subscriptionB)
            receivedB.Add(evt);

        Assert.Single(receivedA);
        Assert.Single(receivedB);
        Assert.Equal("lobby-updated", receivedA[0].EventType);
        Assert.Equal("lobby-updated", receivedB[0].EventType);
    }

    [Fact]
    public async Task PublishAsync_ShouldNotDeliverEvent_ToDifferentGameSubscriber()
    {
        var gameA = Guid.NewGuid();
        var gameB = Guid.NewGuid();

        var subscriptionB = _bus.Subscribe(gameB);
        await _bus.PublishAsync(gameA, "lobby-updated", CreatePayload(gameA));
        _bus.Complete(gameA);
        _bus.Complete(gameB);

        var received = new List<LobbyEvent>();
        await foreach (var evt in subscriptionB)
            received.Add(evt);

        Assert.Empty(received);
    }

    private static GameDto CreatePayload(Guid gameId)
        => new(
            gameId,
            "1234",
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Lobby",
            new GameConfigurationDto(60, 10, 10, 5, 3, true, true),
            [],
            null,
            [],
            null,
            null,
            DateTimeOffset.UtcNow,
            null,
            DateTimeOffset.UtcNow,
            "Unknown",
            null);
}
