using System.Text.Json;
using Azure;
using Azure.Core;
using Azure.Messaging.WebPubSub;
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
    private readonly ILogger<WebPubSubBroadcaster> _logger;

    public WebPubSubBroadcaster(WebPubSubServiceClient client, ILogger<WebPubSubBroadcaster> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task SendToGameAsync(Guid gameId, string eventType, object payload, CancellationToken ct)
    {
        var envelope = JsonSerializer.Serialize(new { type = eventType, data = payload }, SerializerOptions);

        await _client.SendToGroupAsync(
            gameId.ToString(),
            RequestContent.Create(envelope),
            ContentType.ApplicationJson,
            context: new RequestContext { CancellationToken = ct });

        _logger.LogInformation("Sent {EventType} to Web PubSub group {GameId}.", eventType, gameId);
    }
}
