using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;

namespace HexMaster.ThePrey.Maui.App.Services.Api;

/// <summary>
/// <see cref="IPlayFieldApiClient"/> over a typed <see cref="HttpClient"/> whose <c>BaseAddress</c> and
/// timeout are configured in DI. Maps the backend status codes for the authorized playfield endpoints:
/// <c>200</c> → success (including an empty list), <c>400</c> → validation-too-short (search only),
/// <c>401</c> → unauthenticated, network/timeout/other → error. Mirrors <see cref="GameApiClient"/> and
/// <see cref="UserApiClient"/>.
/// </summary>
public sealed class PlayFieldApiClient : IPlayFieldApiClient
{
    private readonly HttpClient _http;
    private readonly ILogger<PlayFieldApiClient> _logger;

    public PlayFieldApiClient(HttpClient http, ILogger<PlayFieldApiClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<MyPlayFieldsResult> GetMyPlayFieldsAsync(string accessToken, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "playfields");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "My-playfields request failed to complete.");
            return MyPlayFieldsResult.Error;
        }

        using (response)
        {
            switch (response.StatusCode)
            {
                case HttpStatusCode.OK:
                    try
                    {
                        var items = await response.Content.ReadFromJsonAsync<PlayFieldSummary[]>(cancellationToken: ct);
                        return MyPlayFieldsResult.Success(items ?? []);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to deserialize my-playfields payload.");
                        return MyPlayFieldsResult.Error;
                    }

                case HttpStatusCode.Unauthorized:
                    return MyPlayFieldsResult.Unauthorized;

                default:
                    _logger.LogWarning("My-playfields endpoint returned unexpected status {Status}.", response.StatusCode);
                    return MyPlayFieldsResult.Error;
            }
        }
    }

    public async Task<PublicPlayFieldsResult> SearchPublicPlayFieldsAsync(string query, string accessToken, CancellationToken ct = default)
    {
        var uri = $"playfields/public?q={Uri.EscapeDataString(query)}";
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Public-playfields search failed to complete.");
            return PublicPlayFieldsResult.Error;
        }

        using (response)
        {
            switch (response.StatusCode)
            {
                case HttpStatusCode.OK:
                    try
                    {
                        var items = await response.Content.ReadFromJsonAsync<PlayFieldSummary[]>(cancellationToken: ct);
                        return PublicPlayFieldsResult.Success(items ?? []);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to deserialize public-playfields payload.");
                        return PublicPlayFieldsResult.Error;
                    }

                case HttpStatusCode.BadRequest:
                    // The backend rejects a query shorter than its minimum length as a validation problem.
                    return PublicPlayFieldsResult.ValidationTooShort;

                case HttpStatusCode.Unauthorized:
                    return PublicPlayFieldsResult.Unauthorized;

                default:
                    _logger.LogWarning("Public-playfields endpoint returned unexpected status {Status}.", response.StatusCode);
                    return PublicPlayFieldsResult.Error;
            }
        }
    }
}
