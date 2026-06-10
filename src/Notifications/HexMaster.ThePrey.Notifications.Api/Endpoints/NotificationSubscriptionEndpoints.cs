using HexMaster.ThePrey.IntegrationEvents;
using HexMaster.ThePrey.IntegrationEvents.Events;
using HexMaster.ThePrey.Notifications;

namespace HexMaster.ThePrey.Notifications.Api.Endpoints;

/// <summary>
/// Dapr pub/sub subscription endpoints. Each integration event topic is delivered here by the Dapr
/// sidecar and forwarded to the matching game's Web PubSub group. These are internal (sidecar → app)
/// calls, so they are anonymous.
/// </summary>
public static class NotificationSubscriptionEndpoints
{
    public static IEndpointRouteBuilder MapNotificationSubscriptionEndpoints(this IEndpointRouteBuilder app)
    {
        MapTopic<PlayerLocationUpdatedIntegrationEvent>(app, IntegrationEventTopics.PlayerLocationUpdated);
        MapTopic<PlayerStatusChangedIntegrationEvent>(app, IntegrationEventTopics.PlayerStatusChanged);
        MapTopic<PlayerPenalizedIntegrationEvent>(app, IntegrationEventTopics.PlayerPenalized);
        MapTopic<GameEndedIntegrationEvent>(app, IntegrationEventTopics.GameEnded);
        return app;
    }

    private static void MapTopic<TEvent>(IEndpointRouteBuilder app, string topic)
        where TEvent : class, IGameScopedIntegrationEvent
    {
        app.MapPost($"/notifications/events/{topic}", async (
                TEvent integrationEvent,
                IWebPubSubBroadcaster broadcaster,
                CancellationToken ct) =>
            {
                await broadcaster.SendToGameAsync(integrationEvent.GameId, integrationEvent.Topic, integrationEvent, ct);
                return Results.Ok();
            })
            .WithTopic(DaprIntegrationEventPublisher.PubSubName, topic)
            .AllowAnonymous();
    }
}
