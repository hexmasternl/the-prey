using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Channels;
using HexMaster.ThePrey.Maui.App.Services.Api;
using HexMaster.ThePrey.Maui.App.Services.Authentication;
using Microsoft.Extensions.Logging;

namespace HexMaster.ThePrey.Maui.App.Services.Realtime;

/// <summary>
/// Default <see cref="IGameStreamClient"/> over Azure Web PubSub. Each (re)connect requests a fresh
/// group-scoped access URL (<c>GET /games/{id}/notifications/token</c>), opens a native WebSocket with
/// the <c>json.webpubsub.azure.v1</c> subprotocol via <see cref="IWebSocketConnectionFactory"/>, sends
/// a <c>joinGroup</c> for the game's group, and — because the native socket does not auto-reconnect —
/// re-runs the whole flow with bounded exponential backoff on an unexpected close, until the
/// subscription's token is cancelled. Group frames are unwrapped to typed events by
/// <see cref="GameStreamEventMapper"/>. The WebSocket and token requests sit behind seams so the
/// envelope mapping and reconnect are unit-testable without a live socket.
/// </summary>
public sealed class GameStreamClient : IGameStreamClient
{
    private const string JsonSubProtocol = "json.webpubsub.azure.v1";

    private static readonly TimeSpan MinBackoff = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan MaxBackoff = TimeSpan.FromSeconds(30);

    private static readonly JsonSerializerOptions FrameOptions = new(JsonSerializerDefaults.Web);

    private readonly IGameApiClient _gameApi;
    private readonly IAccessTokenProvider _accessTokenProvider;
    private readonly IWebSocketConnectionFactory _socketFactory;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<GameStreamClient> _logger;

    public GameStreamClient(
        IGameApiClient gameApi,
        IAccessTokenProvider accessTokenProvider,
        IWebSocketConnectionFactory socketFactory,
        TimeProvider timeProvider,
        ILogger<GameStreamClient> logger)
    {
        _gameApi = gameApi;
        _accessTokenProvider = accessTokenProvider;
        _socketFactory = socketFactory;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async IAsyncEnumerable<GameStreamEvent> Subscribe(
        Guid gameId, string accessToken, [EnumeratorCancellation] CancellationToken ct)
    {
        var channel = Channel.CreateUnbounded<GameStreamEvent>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = true
        });

        var pump = Task.Run(() => PumpAsync(gameId, accessToken, channel.Writer, ct), CancellationToken.None);

        try
        {
            while (true)
            {
                bool more;
                try
                {
                    more = await channel.Reader.WaitToReadAsync(ct);
                }
                catch (OperationCanceledException)
                {
                    break; // Cancellation ends the enumeration cleanly rather than surfacing as a fault.
                }

                if (!more)
                    break;

                while (channel.Reader.TryRead(out var evt))
                    yield return evt;
            }
        }
        finally
        {
            // Enumerator disposed / cancelled: the pump observes ct and completes the channel itself,
            // but await it so the socket is torn down before Subscribe returns.
            try { await pump; }
            catch (OperationCanceledException) { /* expected on teardown */ }
        }
    }

    private async Task PumpAsync(Guid gameId, string accessToken, ChannelWriter<GameStreamEvent> writer, CancellationToken ct)
    {
        var currentToken = accessToken;
        var attempts = 0;

        try
        {
            while (!ct.IsCancellationRequested)
            {
                var (outcome, url, refreshedToken) = await ResolveConnectionUrlAsync(gameId, currentToken, ct);
                currentToken = refreshedToken;

                if (outcome == ConnectResult.TerminalDenied)
                {
                    _logger.LogWarning("Game stream access permanently denied for game {GameId}.", gameId);
                    return;
                }

                if (outcome == ConnectResult.Connected && url is not null
                    && await ConnectAndListenAsync(gameId, url, writer, ct))
                {
                    attempts = 0; // Clean drop after a successful session — reconnect from the minimum delay.
                }

                if (ct.IsCancellationRequested)
                    break;

                attempts++;
                var delay = GameRealtimeConnection.ComputeBackoff(attempts, MinBackoff, MaxBackoff);
                try { await Task.Delay(delay, _timeProvider, ct); }
                catch (OperationCanceledException) { break; }
            }
        }
        catch (OperationCanceledException)
        {
            // Subscription cancelled — fall through to completing the channel.
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unexpected failure in the game stream pump for game {GameId}.", gameId);
        }
        finally
        {
            writer.TryComplete();
        }
    }

    private async Task<(ConnectResult Outcome, string? Url, string Token)> ResolveConnectionUrlAsync(
        Guid gameId, string accessToken, CancellationToken ct)
    {
        var tokenResult = await _gameApi.GetNotificationsTokenAsync(gameId, accessToken, ct);
        switch (tokenResult.Outcome)
        {
            case NotificationsTokenOutcome.Success when !string.IsNullOrWhiteSpace(tokenResult.Url):
                return (ConnectResult.Connected, tokenResult.Url, accessToken);

            case NotificationsTokenOutcome.Forbidden:
                return (ConnectResult.TerminalDenied, null, accessToken);

            case NotificationsTokenOutcome.Unauthorized:
                // The token was rejected; drop it and re-exchange for the next attempt.
                _accessTokenProvider.Invalidate();
                var refreshed = await _accessTokenProvider.GetAccessTokenAsync(ct);
                return (ConnectResult.TransientFailure, null, refreshed ?? accessToken);

            default:
                return (ConnectResult.TransientFailure, null, accessToken);
        }
    }

    /// <summary>Opens the socket, joins the group, and pumps events until the socket drops. Returns true on a clean session.</summary>
    private async Task<bool> ConnectAndListenAsync(
        Guid gameId, string url, ChannelWriter<GameStreamEvent> writer, CancellationToken ct)
    {
        var socket = _socketFactory.Create();
        var joined = false;
        await using (socket.ConfigureAwait(false))
        {
            try
            {
                await socket.ConnectAsync(new Uri(url), JsonSubProtocol, ct);
                await SendJoinGroupAsync(socket, gameId, ct);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to open/join the game stream for game {GameId}; will retry.", gameId);
                return false;
            }

            try
            {
                while (!ct.IsCancellationRequested)
                {
                    string? raw;
                    try
                    {
                        raw = await socket.ReceiveTextAsync(ct);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error reading the game stream for game {GameId}; treating as a drop.", gameId);
                        break;
                    }

                    if (raw is null)
                        break; // Socket closed.

                    foreach (var evt in ParseFrame(raw, ref joined))
                        writer.TryWrite(evt);
                }
            }
            finally
            {
                await CloseQuietlyAsync(socket);
            }
        }

        return joined;
    }

    private static async Task CloseQuietlyAsync(IWebSocketConnection socket)
    {
        try { await socket.CloseAsync(CancellationToken.None); }
        catch { /* best effort */ }
    }

    private Task SendJoinGroupAsync(IWebSocketConnection socket, Guid gameId, CancellationToken ct)
    {
        var frame = JsonSerializer.Serialize(new JoinGroupFrame("joinGroup", gameId.ToString(), 1), FrameOptions);
        return socket.SendTextAsync(frame, ct);
    }

    /// <summary>Parses one transport frame, flipping <paramref name="joined"/> on the join ack and yielding any game events.</summary>
    private IEnumerable<GameStreamEvent> ParseFrame(string raw, ref bool joined)
    {
        JsonElement dataToDispatch = default;
        var hasData = false;

        try
        {
            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return Array.Empty<GameStreamEvent>();

            var type = root.TryGetProperty("type", out var typeEl) ? typeEl.GetString() : null;
            switch (type)
            {
                case "message":
                    if (root.TryGetProperty("from", out var from)
                        && string.Equals(from.GetString(), "group", StringComparison.Ordinal)
                        && root.TryGetProperty("data", out var data))
                    {
                        dataToDispatch = UnwrapEnvelope(data);
                        hasData = dataToDispatch.ValueKind == JsonValueKind.Object;
                    }
                    break;

                case "ack":
                    var success = root.TryGetProperty("success", out var s) && s.ValueKind == JsonValueKind.True;
                    var duplicate = root.TryGetProperty("error", out var err)
                        && err.ValueKind == JsonValueKind.Object
                        && err.TryGetProperty("name", out var name)
                        && string.Equals(name.GetString(), "Duplicate", StringComparison.OrdinalIgnoreCase);
                    if (success || duplicate)
                        joined = true;
                    break;
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse a game stream frame; ignoring it.");
            return Array.Empty<GameStreamEvent>();
        }

        if (!hasData)
            return Array.Empty<GameStreamEvent>();

        return MapEnvelope(dataToDispatch);
    }

    private IEnumerable<GameStreamEvent> MapEnvelope(JsonElement envelope)
    {
        if (!envelope.TryGetProperty("type", out var typeEl) || typeEl.ValueKind != JsonValueKind.String
            || !envelope.TryGetProperty("data", out var payload) || payload.ValueKind != JsonValueKind.Object)
            yield break;

        var evt = GameStreamEventMapper.Map(typeEl.GetString()!, payload);
        if (evt is not null)
            yield return evt;
    }

    /// <summary>The json subprotocol delivers <c>data</c> already parsed, but tolerate a stringified envelope.</summary>
    private JsonElement UnwrapEnvelope(JsonElement data)
    {
        if (data.ValueKind != JsonValueKind.String)
            return data.Clone();

        var inner = data.GetString();
        if (string.IsNullOrEmpty(inner))
            return default;
        try
        {
            using var innerDoc = JsonDocument.Parse(inner);
            return innerDoc.RootElement.Clone();
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse a stringified game stream envelope; ignoring it.");
            return default;
        }
    }

    private enum ConnectResult
    {
        Connected,
        TransientFailure,
        TerminalDenied
    }

    private sealed record JoinGroupFrame(string Type, string Group, int AckId);
}
