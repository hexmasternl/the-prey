using ThePrey.Application.App.Models;

namespace ThePrey.Application.App.Services;

/// <summary>
/// HTTP client abstraction for the PlayFields API.
/// Implementations MUST obtain the access token via <see cref="IAuthService.GetAccessTokenAsync"/>
/// (never read <c>IAuthService.AccessToken</c> directly) so that expired tokens are transparently
/// refreshed before each request.
/// </summary>
public interface IPlayfieldService
{
    Task<IReadOnlyList<Playfield>> GetPlayfieldsAsync(CancellationToken ct = default);
    Task DeletePlayfieldAsync(string id, CancellationToken ct = default);
    Task<IReadOnlyList<Playfield>> SearchPublicPlayfieldsAsync(string query, CancellationToken ct = default);
    Task<Playfield> CreatePlayfieldAsync(Playfield playfield, CancellationToken ct = default);
    Task<Playfield> UpdatePlayfieldAsync(Playfield playfield, CancellationToken ct = default);

    /// <summary>
    /// PUT /playfields/{id} — creates or updates on the server using LWW semantics.
    /// Throws <see cref="StaleWriteException"/> on 409, <see cref="UnauthorizedException"/> on 401.
    /// </summary>
    Task<Playfield> UpsertPlayfieldAsync(Playfield playfield, CancellationToken ct = default);
}
