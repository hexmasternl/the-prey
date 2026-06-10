using System.Diagnostics;
using System.Text.Json;
using Azure;
using Azure.Core;
using Azure.Messaging.WebPubSub;
using HexMaster.ThePrey.Notifications.Observability;
using Microsoft.Extensions.Logging;

namespace HexMaster.ThePrey.Notifications;

/// <summary>
/// Default <see cref="IWebPubSubBroadcaster"/>. Sends each event to the game's group as a JSON envelope
/// <c>{ "type": "&lt;event-topic&gt;", "data": { ... } }</c> so clients can switch on the event type.
/// </summary>
public sealed class WebPubSubBroadcaster : IWebPubSubBroadcaster
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly WebPubSubServiceClient _client;
    private readonly INotificationsMetrics _metrics;
    private readonly ILogger<WebPubSubBroadcaster> _logger;

    public WebPubSubBroadcaster(
        WebPubSubServiceClient client,
        INotificationsMetrics metrics,
        ILogger<WebPubSubBroadcaster> logger)
    {
        _client = client;
        _metrics = metrics;
        _logger = logger;
    }

    public async Task SendToGameAsync(Guid gameId, string eventType, object payload, CancellationToken ct)
    {
        using var activity = NotificationsActivitySource.Source.StartActivity("Notifications.Forward");
        activity?.SetTag("notifications.event_type", eventType);
        activity?.SetTag("game.id", gameId);

        var start = Stopwatch.GetTimestamp();
        try
        {
            var envelope = JsonSerializer.Serialize(new { type = eventType, data = payload }, SerializerOptions);
            activity?.SetTag("notifications.payload_bytes", envelope.Length);

            await _client.SendToGroupAsync(
                gameId.ToString(),
                RequestContent.Create(envelope),
                ContentType.ApplicationJson,
                context: new RequestContext { CancellationToken = ct });

            var elapsedMs = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            _metrics.RecordEventForwarded(eventType, elapsedMs);

            _logger.LogInformation(
                "Forwarded '{EventType}' to Web PubSub group {GameId} in {ElapsedMs:F0}ms.",
                eventType, gameId, elapsedMs);
        }
        catch (Exception ex)
        {
            _metrics.RecordEventForwardFailed(eventType);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            _logger.LogError(ex, "Failed to forward '{EventType}' to Web PubSub group {GameId}.", eventType, gameId);
            throw;
        }
    }
}
