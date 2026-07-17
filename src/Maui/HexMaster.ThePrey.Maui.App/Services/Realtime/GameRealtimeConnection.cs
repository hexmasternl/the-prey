using System.Text.Json;
using HexMaster.ThePrey.Maui.App.Services.Api;
using HexMaster.ThePrey.Maui.App.Services.Authentication;
using Microsoft.Extensions.Logging;

namespace HexMaster.ThePrey.Maui.App.Services.Realtime;

/// <summary>
/// Default <see cref="IGameRealtimeConnection"/>. Mirrors the Ionic client's <c>WebPubSubStream</c>:
/// each (re)connect obtains a fresh group-scoped access URL from <see cref="IGameApiClient"/>, opens a
/// WebSocket with the Web PubSub <c>json</c> subprotocol, sends a <c>joinGroup</c> for the game's group,
/// and — because the native socket does not auto-reconnect — re-runs the whole flow with bounded
/// exponential backoff whenever the socket closes unexpectedly. A terminal <c>403</c> stops the loop and
/// raises <see cref="Unavailable"/>.
/// </summary>
public sealed class GameRealtimeConnection : IGameRealtimeConnection
{
    /// <summary>The Web PubSub "json" subprotocol — lets us send <c>joinGroup</c> and read structured frames.</summary>
    private const string JsonSubProtocol = "json.webpubsub.azure.v1";

    private static readonly TimeSpan MinBackoff = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan MaxBackoff = TimeSpan.FromSeconds(30);

    // camelCase so control frames match the Web PubSub protocol (type/group/ackId).
    private static readonly JsonSerializerOptions FrameOptions = new(JsonSerializerDefaults.Web);

    private readonly IGameApiClient _gameApi;
    private readonly IAccessTokenProvider _accessTokenProvider;
    private readonly IWebSocketConnectionFactory _socketFactory;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<GameRealtimeConnection> _logger;

    private readonly object _lifecycleGate = new();
    private CancellationTokenSource? _cts;
    private Task? _runLoop;
    private Guid _gameId;
    private bool _started;

    private bool _firstConnect = true;
    private int _reconnectAttempts;
    private int _ackIdSeq;

    public event Action<GameRealtimeEnvelope>? EnvelopeReceived;
    public event Action? Connected;
    public event Action? Reconnected;
    public event Action? Unavailable;

    public GameRealtimeConnection(
        IGameApiClient gameApi,
        IAccessTokenProvider accessTokenProvider,
        IWebSocketConnectionFactory socketFactory,
        TimeProvider timeProvider,
        ILogger<GameRealtimeConnection> logger)
    {
        _gameApi = gameApi;
        _accessTokenProvider = accessTokenProvider;
        _socketFactory = socketFactory;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public void Start(Guid gameId)
    {
        lock (_lifecycleGate)
        {
            if (_started)
                return; // Idempotent — one connection per game.
            _started = true;
            _gameId = gameId;
            _firstConnect = true;
            _reconnectAttempts = 0;
            _cts = new CancellationTokenSource();
            _runLoop = Task.Run(() => RunAsync(_cts.Token));
        }
    }

    public async Task StopAsync()
    {
        CancellationTokenSource? cts;
        Task? loop;
        lock (_lifecycleGate)
        {
            if (!_started)
                return;
            _started = false;
            cts = _cts;
            loop = _runLoop;
            _cts = null;
            _runLoop = null;
        }

        cts?.Cancel();
        if (loop is not null)
        {
            try { await loop; }
            catch (OperationCanceledException) { /* expected on teardown */ }
        }
        cts?.Dispose();
    }

    private async Task RunAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            ConnectResult result;
            try
            {
                result = await TryConnectAndListenAsync(ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Unexpected failure in the game real-time loop; will retry.");
                result = ConnectResult.TransientFailure;
            }

            if (ct.IsCancellationRequested)
                break;

            if (result == ConnectResult.TerminalDenied)
            {
                _logger.LogWarning("Game real-time access permanently denied for game {GameId}.", _gameId);
                Unavailable?.Invoke();
                return;
            }

            _reconnectAttempts++;
            var delay = ComputeBackoff(_reconnectAttempts, MinBackoff, MaxBackoff);
            _logger.LogInformation("Reconnecting the game real-time channel in {Delay} (attempt {Attempt}).", delay, _reconnectAttempts);
            try
            {
                await Task.Delay(delay, _timeProvider, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task<ConnectResult> TryConnectAndListenAsync(CancellationToken ct)
    {
        var accessToken = await _accessTokenProvider.GetAccessTokenAsync(ct);
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            _logger.LogWarning("No access token available for the game real-time channel; will retry.");
            return ConnectResult.TransientFailure;
        }

        var tokenResult = await _gameApi.GetNotificationsTokenAsync(_gameId, accessToken!, ct);
        switch (tokenResult.Outcome)
        {
            case NotificationsTokenOutcome.Forbidden:
                return ConnectResult.TerminalDenied;
            case NotificationsTokenOutcome.Unauthorized:
                // The freshly-used token was rejected; drop it so the next attempt re-exchanges.
                _accessTokenProvider.Invalidate();
                return ConnectResult.TransientFailure;
            case NotificationsTokenOutcome.Success when !string.IsNullOrWhiteSpace(tokenResult.Url):
                break;
            default:
                return ConnectResult.TransientFailure;
        }

        var socket = _socketFactory.Create();
        await using (socket.ConfigureAwait(false))
        {
            try
            {
                await socket.ConnectAsync(new Uri(tokenResult.Url!), JsonSubProtocol, ct);
                await SendJoinGroupAsync(socket, ct);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to open/join the game real-time channel; will retry.");
                await CloseQuietlyAsync(socket);
                return ConnectResult.TransientFailure;
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
                        _logger.LogWarning(ex, "Error reading from the game real-time channel; treating as a drop.");
                        break;
                    }

                    if (raw is null)
                        break; // Socket closed.

                    HandleRawMessage(raw);
                }
            }
            finally
            {
                await CloseQuietlyAsync(socket);
            }
        }

        return ConnectResult.Dropped;
    }

    /// <summary>Subscribe to this game's group so the server's broadcasts reach us.</summary>
    private Task SendJoinGroupAsync(IWebSocketConnection socket, CancellationToken ct)
    {
        var ackId = Interlocked.Increment(ref _ackIdSeq);
        var frame = JsonSerializer.Serialize(new JoinGroupFrame("joinGroup", _gameId.ToString(), ackId), FrameOptions);
        return socket.SendTextAsync(frame, ct);
    }

    private void HandleRawMessage(string raw)
    {
        JsonElement root;
        try
        {
            using var doc = JsonDocument.Parse(raw);
            // Handle inside the using so any element we forward is Clone()d before disposal.
            root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return;

            var type = root.TryGetProperty("type", out var typeEl) ? typeEl.GetString() : null;
            switch (type)
            {
                case "message":
                    if (root.TryGetProperty("from", out var from)
                        && string.Equals(from.GetString(), "group", StringComparison.Ordinal)
                        && root.TryGetProperty("data", out var data))
                    {
                        DispatchGroupData(data);
                    }
                    break;

                case "ack":
                    var success = root.TryGetProperty("success", out var s) && s.ValueKind == JsonValueKind.True;
                    var duplicate = root.TryGetProperty("error", out var err)
                        && err.ValueKind == JsonValueKind.Object
                        && err.TryGetProperty("name", out var name)
                        && string.Equals(name.GetString(), "Duplicate", StringComparison.OrdinalIgnoreCase);
                    if (success || duplicate)
                        OnJoined();
                    else
                        _logger.LogWarning("joinGroup ack reported failure: {Frame}", raw);
                    break;

                case "system":
                    _logger.LogDebug("Web PubSub system frame: {Frame}", raw);
                    break;
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse a game real-time frame; ignoring it.");
        }
    }

    private void DispatchGroupData(JsonElement data)
    {
        JsonElement envelope = data;

        // With the json subprotocol the server's ApplicationJson arrives already parsed, but be defensive
        // in case it comes through as a JSON string.
        if (data.ValueKind == JsonValueKind.String)
        {
            var inner = data.GetString();
            if (string.IsNullOrEmpty(inner))
                return;
            try
            {
                using var innerDoc = JsonDocument.Parse(inner);
                envelope = innerDoc.RootElement.Clone();
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to parse a stringified game real-time envelope; ignoring it.");
                return;
            }
        }

        if (envelope.ValueKind != JsonValueKind.Object
            || !envelope.TryGetProperty("type", out var typeEl)
            || typeEl.ValueKind != JsonValueKind.String)
        {
            _logger.LogDebug("Group message without a string 'type'; ignoring it.");
            return;
        }

        var eventType = typeEl.GetString()!;
        var payload = envelope.TryGetProperty("data", out var d) ? d.Clone() : default;

        var version = envelope.TryGetProperty("v", out var vEl) && vEl.ValueKind == JsonValueKind.Number
            ? vEl.GetInt32()
            : (int?)null;
        var seq = envelope.TryGetProperty("seq", out var seqEl) && seqEl.ValueKind == JsonValueKind.Number
            ? seqEl.GetInt64()
            : (long?)null;
        var gameId = envelope.TryGetProperty("gameId", out var gameIdEl)
            && gameIdEl.ValueKind == JsonValueKind.String
            && Guid.TryParse(gameIdEl.GetString(), out var parsedGameId)
                ? parsedGameId
                : (Guid?)null;

        EnvelopeReceived?.Invoke(new GameRealtimeEnvelope(eventType, payload, version, seq, gameId));
    }

    private void OnJoined()
    {
        _reconnectAttempts = 0;
        if (_firstConnect)
        {
            _firstConnect = false;
            _logger.LogInformation("Game real-time channel connected & joined group {GameId}.", _gameId);
            Connected?.Invoke();
        }
        else
        {
            _logger.LogInformation("Game real-time channel reconnected & re-joined group {GameId}.", _gameId);
            Reconnected?.Invoke();
        }
    }

    private static async Task CloseQuietlyAsync(IWebSocketConnection socket)
    {
        try { await socket.CloseAsync(CancellationToken.None); }
        catch { /* best effort */ }
    }

    /// <summary>Exponential backoff: <c>min * 2^(attempt-1)</c>, clamped to <paramref name="max"/>.</summary>
    internal static TimeSpan ComputeBackoff(int attempt, TimeSpan min, TimeSpan max)
    {
        if (attempt <= 1)
            return min;
        var exponent = Math.Min(attempt - 1, 16); // cap the shift so the multiply cannot overflow.
        var scaled = min * Math.Pow(2, exponent);
        return scaled >= max ? max : scaled;
    }

    private enum ConnectResult
    {
        /// <summary>Connected and later dropped — reconnect from the minimum delay.</summary>
        Dropped,
        /// <summary>Could not connect this attempt — back off and retry.</summary>
        TransientFailure,
        /// <summary>Access permanently denied — stop and report unavailable.</summary>
        TerminalDenied,
    }

    private sealed record JoinGroupFrame(string Type, string Group, int AckId);
}
