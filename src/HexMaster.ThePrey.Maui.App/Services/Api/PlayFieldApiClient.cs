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

    public async Task<CreatePlayFieldResult> CreatePlayFieldAsync(
        string name,
        bool isPublic,
        IReadOnlyList<GpsCoordinate> points,
        string accessToken,
        CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "playfields")
        {
            Content = JsonContent.Create(new CreatePlayFieldBody(name, isPublic, points))
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Create-playfield request failed to complete.");
            return CreatePlayFieldResult.Error;
        }

        using (response)
        {
            switch (response.StatusCode)
            {
                case HttpStatusCode.Created:
                    try
                    {
                        var created = await response.Content.ReadFromJsonAsync<CreatedPlayFieldBody>(cancellationToken: ct);
                        return created is null
                            ? CreatePlayFieldResult.Error
                            : CreatePlayFieldResult.Success(new PlayFieldSummary(created.Id, created.Name, created.IsPublic));
                    }
                    catch (Exception ex)
                    {
                        // The server did create the record, but its payload could not be projected —
                        // surface an error so the create page falls back to reloading the list.
                        _logger.LogWarning(ex, "Failed to deserialize created-playfield payload.");
                        return CreatePlayFieldResult.Error;
                    }

                case HttpStatusCode.BadRequest:
                    return CreatePlayFieldResult.Validation;

                case HttpStatusCode.Unauthorized:
                    return CreatePlayFieldResult.Unauthorized;

                default:
                    _logger.LogWarning("Create-playfield endpoint returned unexpected status {Status}.", response.StatusCode);
                    return CreatePlayFieldResult.Error;
            }
        }
    }

    public async Task<DeletePlayFieldResult> DeletePlayFieldAsync(Guid id, string accessToken, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"playfields/{id}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Delete-playfield request failed to complete.");
            return DeletePlayFieldResult.Error;
        }

        using (response)
        {
            switch (response.StatusCode)
            {
                case HttpStatusCode.NoContent:
                    return DeletePlayFieldResult.Success;

                case HttpStatusCode.NotFound:
                    return DeletePlayFieldResult.NotFound;

                case HttpStatusCode.Forbidden:
                    return DeletePlayFieldResult.Forbidden;

                case HttpStatusCode.Unauthorized:
                    return DeletePlayFieldResult.Unauthorized;

                default:
                    _logger.LogWarning("Delete-playfield endpoint returned unexpected status {Status}.", response.StatusCode);
                    return DeletePlayFieldResult.Error;
            }
        }
    }

    public async Task<GetPlayFieldResult> GetPlayFieldAsync(Guid id, string accessToken, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"playfields/{id}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Get-playfield request failed to complete.");
            return GetPlayFieldResult.Error;
        }

        using (response)
        {
            switch (response.StatusCode)
            {
                case HttpStatusCode.OK:
                    try
                    {
                        var body = await response.Content.ReadFromJsonAsync<FullPlayFieldBody>(cancellationToken: ct);
                        return body is null ? GetPlayFieldResult.Error : GetPlayFieldResult.Success(body.ToDetails());
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to deserialize get-playfield payload.");
                        return GetPlayFieldResult.Error;
                    }

                case HttpStatusCode.NotFound:
                    return GetPlayFieldResult.NotFound;

                case HttpStatusCode.Unauthorized:
                    return GetPlayFieldResult.Unauthorized;

                default:
                    _logger.LogWarning("Get-playfield endpoint returned unexpected status {Status}.", response.StatusCode);
                    return GetPlayFieldResult.Error;
            }
        }
    }

    public async Task<UpdatePlayFieldResult> UpdatePlayFieldAsync(
        Guid id,
        string name,
        bool isPublic,
        IReadOnlyList<GpsCoordinate> points,
        DateTimeOffset lastUpdatedOn,
        string accessToken,
        CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, $"playfields/{id}")
        {
            Content = JsonContent.Create(new UpsertPlayFieldBody(name, isPublic, points, lastUpdatedOn))
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Update-playfield request failed to complete.");
            return UpdatePlayFieldResult.Error;
        }

        using (response)
        {
            switch (response.StatusCode)
            {
                case HttpStatusCode.OK:
                    try
                    {
                        var updated = await response.Content.ReadFromJsonAsync<CreatedPlayFieldBody>(cancellationToken: ct);
                        return updated is null
                            ? UpdatePlayFieldResult.Error
                            : UpdatePlayFieldResult.Updated(new PlayFieldSummary(updated.Id, updated.Name, updated.IsPublic));
                    }
                    catch (Exception ex)
                    {
                        // The server did persist the update, but its payload could not be projected —
                        // surface an error so the edit page falls back to reloading the list.
                        _logger.LogWarning(ex, "Failed to deserialize updated-playfield payload.");
                        return UpdatePlayFieldResult.Error;
                    }

                case HttpStatusCode.Conflict:
                    try
                    {
                        var current = await response.Content.ReadFromJsonAsync<FullPlayFieldBody>(cancellationToken: ct);
                        return current is null
                            ? UpdatePlayFieldResult.Error
                            : UpdatePlayFieldResult.Conflict(current.ToDetails());
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to deserialize conflict-playfield payload.");
                        return UpdatePlayFieldResult.Error;
                    }

                case HttpStatusCode.BadRequest:
                    return UpdatePlayFieldResult.Validation;

                case HttpStatusCode.Unauthorized:
                    return UpdatePlayFieldResult.Unauthorized;

                case HttpStatusCode.Forbidden:
                    return UpdatePlayFieldResult.Forbidden;

                case HttpStatusCode.NotFound:
                    return UpdatePlayFieldResult.NotFound;

                default:
                    _logger.LogWarning("Update-playfield endpoint returned unexpected status {Status}.", response.StatusCode);
                    return UpdatePlayFieldResult.Error;
            }
        }
    }

    // Request body — serialized with the default web (camelCase) options to the backend's
    // CreatePlayFieldRequest { name, isPublic, points: [{ latitude, longitude }] } shape.
    private sealed record CreatePlayFieldBody(string Name, bool IsPublic, IReadOnlyList<GpsCoordinate> Points);

    // PUT body — the backend's UpsertPlayFieldRequest { name, isPublic, points, lastUpdatedOn } shape.
    private sealed record UpsertPlayFieldBody(
        string Name, bool IsPublic, IReadOnlyList<GpsCoordinate> Points, DateTimeOffset LastUpdatedOn);

    // Only the fields the client needs off the 200/201 PlayFieldDto; extra fields are ignored.
    private sealed record CreatedPlayFieldBody(Guid Id, string Name, bool IsPublic);

    // The full PlayFieldDto projection (points + stamp) for get-by-id and 409 conflict bodies.
    private sealed record FullPlayFieldBody(
        Guid Id, string Name, bool IsPublic, List<CoordinateBody>? Points, DateTimeOffset LastUpdatedOn)
    {
        public PlayFieldDetails ToDetails() => new(
            Id,
            Name,
            IsPublic,
            (Points ?? []).Select(p => new GpsCoordinate(p.Latitude, p.Longitude)).ToList(),
            LastUpdatedOn);
    }

    private sealed record CoordinateBody(double Latitude, double Longitude);
}
