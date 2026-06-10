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
        // Typed events from the game sweep — forwarded under their own topic name.
        MapTopic<PlayerLocationUpdatedIntegrationEvent>(app, IntegrationEventTopics.PlayerLocationUpdated);
        MapTopic<PlayerStatusChangedIntegrationEvent>(app, IntegrationEventTopics.PlayerStatusChanged);
        MapTopic<PlayerPenalizedIntegrationEvent>(app, IntegrationEventTopics.PlayerPenalized);
        MapTopic<GameEndedIntegrationEvent>(app, IntegrationEventTopics.GameEnded);

        // Envelope events bridged from the in-process game/lobby buses — forwarded under the inner
        // event name with the original payload, so clients receive them exactly as before.
        MapEnvelopeTopic<GameNotificationIntegrationEvent>(app, IntegrationEventTopics.GameNotification, e => e.Name, e => e.Payload, e => e.GameId);
        MapEnvelopeTopic<LobbyNotificationIntegrationEvent>(app, IntegrationEventTopics.LobbyNotification, e => e.Name, e => e.Payload, e => e.GameId);
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

    // Forwards an envelope event under its inner name + payload, so clients receive the original event
    // verbatim (matching what the in-process bus / former SSE stream delivered).
    private static void MapEnvelopeTopic<TEvent>(
        IEndpointRouteBuilder app,
        string topic,
        Func<TEvent, string> nameSelector,
        Func<TEvent, object> payloadSelector,
        Func<TEvent, Guid> gameIdSelector)
        where TEvent : class, IGameScopedIntegrationEvent
    {
        app.MapPost($"/notifications/events/{topic}", async (
                TEvent integrationEvent,
                IWebPubSubBroadcaster broadcaster,
                CancellationToken ct) =>
            {
                await broadcaster.SendToGameAsync(
                    gameIdSelector(integrationEvent),
                    nameSelector(integrationEvent),
                    payloadSelector(integrationEvent),
                    ct);
                return Results.Ok();
            })
            .WithTopic(DaprIntegrationEventPublisher.PubSubName, topic)
            .AllowAnonymous();
    }
}
