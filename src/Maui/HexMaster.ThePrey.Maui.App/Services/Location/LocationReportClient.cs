using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;

namespace HexMaster.ThePrey.Maui.App.Services.Location;

/// <summary>
/// <see cref="ILocationReportClient"/> over a typed <see cref="HttpClient"/> whose <c>BaseAddress</c> and
/// timeout are configured in DI. Maps the backend status codes for <c>POST /games/{id}/locations</c>:
/// <c>200</c> → accepted (+ next cadence), <c>404</c>/<c>422</c> → game not InProgress (stop signal),
/// <c>401</c> → token refresh needed, <c>5xx</c>/network/timeout → transient (retry next tick).
/// Analogous to <see cref="Api.GameApiClient"/>.
/// </summary>
public sealed class LocationReportClient : ILocationReportClient
{
    private const int UnprocessableContent = 422;

    private readonly HttpClient _http;
    private readonly ILogger<LocationReportClient> _logger;

    public LocationReportClient(HttpClient http, ILogger<LocationReportClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<LocationReportResult> ReportAsync(
        Guid gameId, RecordLocationRequest request, string accessToken, CancellationToken ct = default)
    {
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"games/{gameId}/locations")
        {
            Content = JsonContent.Create(request)
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(httpRequest, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            _logger.LogInformation(ex, "Location report failed to reach the backend — retrying next tick.");
            return LocationReportResult.Transient;
        }

        using (response)
        {
            switch (response.StatusCode)
            {
                case HttpStatusCode.OK:
                    try
                    {
                        var body = await response.Content.ReadFromJsonAsync<RecordLocationResponse>(cancellationToken: ct);
                        return body is null ? LocationReportResult.Transient : LocationReportResult.Accepted(body);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to deserialize the location-report response.");
                        return LocationReportResult.Transient;
                    }

                case HttpStatusCode.NotFound:
                    return LocationReportResult.GameOver;

                case HttpStatusCode.Unauthorized:
                    return LocationReportResult.Unauthorized;

                default:
                    if ((int)response.StatusCode == UnprocessableContent)
                        return LocationReportResult.GameOver;

                    // 5xx and any other unexpected status: treat as transient and retry on the next tick.
                    _logger.LogWarning("Location-report endpoint returned {Status}; retrying next tick.", response.StatusCode);
                    return LocationReportResult.Transient;
            }
        }
    }
}
