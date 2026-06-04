using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ThePrey.Application.App.Models;

namespace ThePrey.Application.App.Services;

public sealed class GameService(IHttpClientFactory httpClientFactory, IAuthService authService) : IGameService
{
    private async Task<HttpClient> CreateClientAsync(CancellationToken ct = default)
    {
        var client = httpClientFactory.CreateClient("games");
        var token = await authService.GetAccessTokenAsync(ct);
        if (token is null)
            throw new UnauthorizedException();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    public async Task<Game> CreateGameAsync(CreateGameOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        var client = await CreateClientAsync(ct);
        var response = await client.PostAsJsonAsync(
            "games",
            new
            {
                playfieldId = options.PlayfieldId,
                displayName = options.DisplayName,
                profilePictureUrl = options.ProfilePictureUrl,
                gameDuration = options.GameDurationMinutes,
                hunterDelayTime = options.HunterDelayMinutes,
                finalStageDuration = options.FinalStageMinutes,
                // The API expects the location intervals in seconds; the pickers are in minutes.
                defaultLocationInterval = options.DefaultLocationIntervalMinutes * 60,
                finalLocationInterval = options.FinalLocationIntervalMinutes * 60,
                enablePreyBoundaryPenalties = options.EnablePreyBoundaryPenalty,
                enableHunterBoundaryPenalty = options.EnableHunterBoundaryPenalty,
            },
            ct);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
            throw new UnauthorizedException();

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Game>(ct)
            ?? throw new InvalidOperationException("The server returned an empty game.");
    }

    public async Task<Game?> GetGameAsync(Guid gameId, CancellationToken ct = default)
    {
        var client = await CreateClientAsync(ct);
        var response = await client.GetAsync($"games/{gameId}", ct);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
            throw new UnauthorizedException();
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Game>(ct);
    }

    public async Task<Game?> StartGameAsync(Guid gameId, Guid hunterUserId, CancellationToken ct = default)
    {
        var client = await CreateClientAsync(ct);
        var response = await client.PostAsJsonAsync($"games/{gameId}/start", new { hunterUserId }, ct);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
            throw new UnauthorizedException();
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Game>(ct);
    }

    public async Task<LocationPushResponse?> PushLocationAsync(
        string gameId, double latitude, double longitude, double? accuracy, CancellationToken ct = default)
    {
        var client = await CreateClientAsync(ct);
        var response = await client.PostAsJsonAsync(
            $"games/{gameId}/locations",
            new { latitude, longitude, accuracy },
            ct);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
            throw new UnauthorizedException();
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<LocationPushResponse>(ct);
    }

    public async Task<GameStateSnapshot?> GetGameStateAsync(string gameId, CancellationToken ct = default)
    {
        var client = await CreateClientAsync(ct);
        var response = await client.GetAsync($"games/{gameId}/state", ct);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
            throw new UnauthorizedException();
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<GameStateSnapshot>(ct);
    }
}
