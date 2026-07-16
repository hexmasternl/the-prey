using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace HexMaster.ThePrey.Maui.App.Services.Api;

/// <summary>
/// <see cref="ILobbyStreamClient"/> over the backend's Server-Sent-Events lobby stream
/// (<c>GET /games/{id}/lobby/stream</c>). Parses <c>event:</c>/<c>data:</c> frames, skips
/// <c>heartbeat</c> events (and SSE comment lines), deserializes each real event's <c>data</c> into a
/// <see cref="GameDetails"/> snapshot, and reconnects after a short delay whenever the stream drops —
/// until the caller cancels. The typed <see cref="HttpClient"/>'s <c>BaseAddress</c> is configured in DI.
/// </summary>
public sealed class LobbyStreamClient : ILobbyStreamClient
{
    /// <summary>Delay before re-opening a dropped stream, so a flapping connection does not busy-loop.</summary>
    private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(3);

    // Case-insensitive so the backend's camelCase snapshot binds onto the PascalCase projection.
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly ILogger<LobbyStreamClient> _logger;

    public LobbyStreamClient(HttpClient http, ILogger<LobbyStreamClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async IAsyncEnumerable<GameDetails> Subscribe(
        Guid gameId, string accessToken, [EnumeratorCancellation] CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var connected = false;
            IAsyncEnumerator<GameDetails>? enumerator = null;
            try
            {
                enumerator = ReadStreamAsync(gameId, accessToken, ct).GetAsyncEnumerator(ct);
            }
            catch (OperationCanceledException)
            {
                yield break;
            }

            try
            {
                while (true)
                {
                    GameDetails snapshot;
                    try
                    {
                        if (!await enumerator.MoveNextAsync())
                            break;
                        connected = true;
                        snapshot = enumerator.Current;
                    }
                    catch (OperationCanceledException)
                    {
                        yield break;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Lobby stream for game {GameId} dropped; will reconnect.", gameId);
                        break;
                    }

                    yield return snapshot;
                }
            }
            finally
            {
                if (enumerator is not null)
                    await enumerator.DisposeAsync();
            }

            if (ct.IsCancellationRequested)
                yield break;

            // Reconnect after a short pause. The pause is skipped-cancellable so teardown stays prompt.
            _ = connected; // reconnect regardless of whether this attempt yielded anything.
            try
            {
                await Task.Delay(ReconnectDelay, ct);
            }
            catch (OperationCanceledException)
            {
                yield break;
            }
        }
    }

    // Opens one SSE connection and yields a snapshot per real (non-heartbeat) event until it ends.
    private async IAsyncEnumerable<GameDetails> ReadStreamAsync(
        Guid gameId, string accessToken, [EnumeratorCancellation] CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"games/{gameId}/lobby/stream");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        string? eventType = null;
        var data = new System.Text.StringBuilder();

        while (!ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct);
            if (line is null)
                break; // Stream ended.

            if (line.Length == 0)
            {
                // Blank line dispatches the accumulated frame.
                var snapshot = TryBuildSnapshot(eventType, data.ToString());
                eventType = null;
                data.Clear();
                if (snapshot is not null)
                    yield return snapshot;
                continue;
            }

            if (line[0] == ':')
                continue; // SSE comment (e.g. the ": connected" preamble).

            if (line.StartsWith("event:", StringComparison.Ordinal))
                eventType = line["event:".Length..].Trim();
            else if (line.StartsWith("data:", StringComparison.Ordinal))
                data.Append(line["data:".Length..].TrimStart());
        }
    }

    // Deserializes a real event's data into a snapshot; heartbeats and unparseable frames yield null.
    private GameDetails? TryBuildSnapshot(string? eventType, string data)
    {
        if (string.Equals(eventType, "heartbeat", StringComparison.OrdinalIgnoreCase))
            return null;
        if (string.IsNullOrWhiteSpace(data))
            return null;

        try
        {
            return JsonSerializer.Deserialize<GameDetails>(data, JsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize a lobby-stream snapshot; skipping the frame.");
            return null;
        }
    }
}
