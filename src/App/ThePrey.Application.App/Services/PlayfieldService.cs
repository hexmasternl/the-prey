using System.Net;
using System.Net.Http.Headers;
using ThePrey.Application.App.Models;

namespace ThePrey.Application.App.Services;

public sealed class PlayfieldService(IHttpClientFactory httpClientFactory, IAuthService authService) : IPlayfieldService
{
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
}
