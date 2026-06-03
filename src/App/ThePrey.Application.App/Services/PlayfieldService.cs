using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ThePrey.Application.App.Models;

namespace ThePrey.Application.App.Services;

public sealed class PlayfieldService(IHttpClientFactory httpClientFactory, IAuthService authService) : IPlayfieldService
{
    // Serializer that excludes IsSynchronized when sending to the server.
    private static readonly JsonSerializerOptions ServerSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private HttpClient CreateClient()
    {
        var client = httpClientFactory.CreateClient("playfields");
        if (authService.AccessToken is { } token)
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    public async Task<IReadOnlyList<Playfield>> GetPlayfieldsAsync(CancellationToken ct = default)
    {
        var client = CreateClient();
        var response = await client.GetAsync("playfields", ct);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
            throw new UnauthorizedException();
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<Playfield>>(ct) ?? [];
    }

    public async Task DeletePlayfieldAsync(string id, CancellationToken ct = default)
    {
        var client = CreateClient();
        var response = await client.DeleteAsync($"playfields/{id}", ct);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
            throw new UnauthorizedException();
        response.EnsureSuccessStatusCode();
    }

    public async Task<IReadOnlyList<Playfield>> SearchPublicPlayfieldsAsync(string query, CancellationToken ct = default)
    {
        try
        {
            var client = CreateClient();
            var response = await client.GetAsync(
                $"playfields/public?q={Uri.EscapeDataString(query)}", ct);
            if (response.StatusCode == HttpStatusCode.Unauthorized)
                throw new UnauthorizedException();
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<List<Playfield>>(ct) ?? [];
        }
        catch (OperationCanceledException)
        {
            return [];
        }
    }

    public async Task<Playfield> CreatePlayfieldAsync(Playfield playfield, CancellationToken ct = default)
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync("playfields", playfield, ct);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
            throw new UnauthorizedException();
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Playfield>(ct) ?? playfield;
    }

    public async Task<Playfield> UpdatePlayfieldAsync(Playfield playfield, CancellationToken ct = default)
    {
        var client = CreateClient();
        var response = await client.PutAsJsonAsync($"playfields/{playfield.Id}", playfield, ct);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
            throw new UnauthorizedException();
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Playfield>(ct) ?? playfield;
    }

    public async Task<Playfield> UpsertPlayfieldAsync(Playfield playfield, CancellationToken ct = default)
    {
        var payload = new
        {
            playfield.Name,
            playfield.IsPublic,
            Coordinates = playfield.Coordinates,
            LastUpdatedOn = playfield.LastUpdatedOn
        };

        var client = CreateClient();
        var response = await client.PutAsJsonAsync($"playfields/{playfield.Id}", payload, ct);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
            throw new UnauthorizedException();

        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            var serverCopy = await response.Content.ReadFromJsonAsync<Playfield>(ct) ?? playfield;
            throw new StaleWriteException(serverCopy);
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Playfield>(ct) ?? playfield;
    }
}
