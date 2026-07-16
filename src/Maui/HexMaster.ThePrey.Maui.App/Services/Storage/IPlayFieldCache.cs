using HexMaster.ThePrey.Maui.App.Services.Api;

namespace HexMaster.ThePrey.Maui.App.Services.Storage;

/// <summary>
/// On-device display cache of the current user's private playfields — the seam that makes the Private
/// tab local-first. It stores only the private list (a read-through display cache; the server stays
/// authoritative). No MAUI or file types leak to callers, so the view model stays unit-testable.
/// </summary>
public interface IPlayFieldCache
{
    /// <summary>
    /// Returns the last-cached private playfields, or an empty list when nothing is cached or the cache
    /// cannot be read. Never throws.
    /// </summary>
    Task<IReadOnlyList<PlayFieldSummary>> LoadAsync(CancellationToken ct = default);

    /// <summary>Overwrites the cached private playfields with <paramref name="items"/>.</summary>
    Task SaveAsync(IReadOnlyList<PlayFieldSummary> items, CancellationToken ct = default);
}
