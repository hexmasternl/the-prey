using ThePrey.Application.App.Models;

namespace ThePrey.Application.App.Services;

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
