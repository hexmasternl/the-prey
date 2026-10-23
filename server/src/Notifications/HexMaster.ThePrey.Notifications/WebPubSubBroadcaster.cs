using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Azure;
using Azure.Core;
using Azure.Messaging.WebPubSub;
using HexMaster.ThePrey.IntegrationEvents;
using HexMaster.ThePrey.Notifications.Observability;
using Microsoft.Extensions.Logging;

namespace HexMaster.ThePrey.Notifications;

/// <summary>
/// Default <see cref="IWebPubSubBroadcaster"/>. Sends each message to the game's group as the canonical
/// versioned envelope <c>{ v, type, gameId, seq, data }</c> (camelCase) so clients can switch on the
/// message type, reject an unsupported protocol version, and detect dropped messages via <c>seq</c>.
/// Registered as a singleton so the per-game <c>seq</c> counter survives across requests.
/// </summary>
public sealed class WebPubSubBroadcaster : IWebPubSubBroadcaster
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    // One monotonic sequence counter per game. Boxed long so we can Interlocked.Increment in place.
    private readonly ConcurrentDictionary<Guid, StrongBox<long>> _sequences = new();

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

    public Task SendToGameAsync(Guid gameId, string eventType, object payload, CancellationToken ct)
        => BroadcastAsync(gameId, eventType, payload, ct);

    public Task RequestResyncAsync(Guid gameId, string reason, CancellationToken ct)
        => BroadcastAsync(gameId, RealtimeProtocol.MessageTypes.ResyncRequested, new { reason }, ct);

    private async Task BroadcastAsync(Guid gameId, string eventType, object payload, CancellationToken ct)
    {
        using var activity = NotificationsActivitySource.Source.StartActivity("Notifications.Forward");
        activity?.SetTag("notifications.event_type", eventType);
        activity?.SetTag("notifications.protocol_version", RealtimeProtocol.Version);
        activity?.SetTag("game.id", gameId);

        var start = Stopwatch.GetTimestamp();
        try
        {
            var seq = NextSequence(gameId);
            activity?.SetTag("notifications.seq", seq);

            var envelope = JsonSerializer.Serialize(
                new
                {
                    v = RealtimeProtocol.Version,
                    type = eventType,
                    gameId,
                    seq,
                    data = payload
                },
                SerializerOptions);
            activity?.SetTag("notifications.payload_bytes", envelope.Length);

            await _client.SendToGroupAsync(
                gameId.ToString(),
                RequestContent.Create(envelope),
                ContentType.ApplicationJson,
                context: new RequestContext { CancellationToken = ct });

            var elapsedMs = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            _metrics.RecordEventForwarded(eventType, elapsedMs);

            _logger.LogInformation(
                "Forwarded '{EventType}' (seq {Seq}) to Web PubSub group {GameId} in {ElapsedMs:F0}ms.",
                eventType, seq, gameId, elapsedMs);
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

    private long NextSequence(Guid gameId)
    {
        var box = _sequences.GetOrAdd(gameId, static _ => new StrongBox<long>(0));
        return Interlocked.Increment(ref box.Value);
    }
}
