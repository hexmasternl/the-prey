using ThePrey.Application.App.Models;

namespace ThePrey.Application.App.Services;

public interface IPlayfieldService
{
    Task<IReadOnlyList<Playfield>> GetPlayfieldsAsync(CancellationToken ct = default);
    Task DeletePlayfieldAsync(string id, CancellationToken ct = default);
}
