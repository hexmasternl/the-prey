using Dapr.Client;
using HexMaster.ThePrey.IntegrationEvents;
using HexMaster.ThePrey.IntegrationEvents.Events;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace HexMaster.ThePrey.IntegrationEvents.Tests;

public sealed class DaprIntegrationEventPublisherTests
{
    [Fact]
    public async Task PublishAsync_ShouldPublishToConfiguredPubSubAndEventTopic_WhenInvoked()
    {
        var dapr = new Mock<DaprClient>();
        dapr.Setup(d => d.PublishEventAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var sut = new DaprIntegrationEventPublisher(dapr.Object, NullLogger<DaprIntegrationEventPublisher>.Instance);
        var evt = new GameNotificationIntegrationEvent(Guid.NewGuid(), "prey-updated", new { });

        await sut.PublishAsync(evt, CancellationToken.None);

        dapr.Verify(d => d.PublishEventAsync(
                DaprIntegrationEventPublisher.PubSubName,
                IntegrationEventTopics.GameNotification,
                It.Is<object>(o => ReferenceEquals(o, evt)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [MemberData(nameof(EventTopicCases))]
    public void Topic_ShouldMatchCatalog_ForEachEventType(IIntegrationEvent evt, string expectedTopic)
        => Assert.Equal(expectedTopic, evt.Topic);

    public static TheoryData<IIntegrationEvent, string> EventTopicCases() => new()
    {
        { new GameNotificationIntegrationEvent(Guid.NewGuid(), "prey-updated", new { }), IntegrationEventTopics.GameNotification },
        { new LobbyNotificationIntegrationEvent(Guid.NewGuid(), "participant-joined", new { }), IntegrationEventTopics.LobbyNotification },
    };
}
