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
        // Envelope events bridged from the in-process game/lobby buses — forwarded under the inner
        // event name with the original payload, so clients receive them exactly as before.
        MapEnvelopeTopic<GameNotificationIntegrationEvent>(app, IntegrationEventTopics.GameNotification, e => e.Name, e => e.Payload, e => e.GameId);
        MapEnvelopeTopic<LobbyNotificationIntegrationEvent>(app, IntegrationEventTopics.LobbyNotification, e => e.Name, e => e.Payload, e => e.GameId);
        return app;
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
                ILoggerFactory loggerFactory,
                CancellationToken ct) =>
            {
                var gameId = gameIdSelector(integrationEvent);
                var name = nameSelector(integrationEvent);
                loggerFactory.CreateLogger("Notifications.Subscription")
                    .LogDebug("Received envelope '{Topic}' (inner '{Name}') for game {GameId} from pub/sub.", topic, name, gameId);
                await broadcaster.SendToGameAsync(gameId, name, payloadSelector(integrationEvent), ct);
                return Results.Ok();
            })
            .WithTopic(DaprIntegrationEventPublisher.PubSubName, topic)
            .AllowAnonymous();
    }
}
