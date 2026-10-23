using HexMaster.ThePrey.Games.DomainModels;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace HexMaster.ThePrey.Games.GameEngine;

/// <summary>
/// Caches playfield boundary polygons (immutable per game) so the sweep fetches each one once instead
/// of on every tick. <see cref="IPlayfieldInfoProvider"/> is scoped (it uses a Dapr client), so this
/// singleton resolves it inside a fresh scope on a cache miss.
/// </summary>
public sealed class CachingPlayfieldBoundaryProvider : IPlayfieldBoundaryProvider
{
    private static readonly IReadOnlyList<GpsCoordinate> Empty = Array.Empty<GpsCoordinate>();

    private readonly IMemoryCache _cache;
    private readonly IServiceScopeFactory _scopeFactory;

    public CachingPlayfieldBoundaryProvider(IMemoryCache cache, IServiceScopeFactory scopeFactory)
    {
        _cache = cache;
        _scopeFactory = scopeFactory;
    }

    public async Task<IReadOnlyList<GpsCoordinate>> GetPolygonAsync(Guid playfieldId, CancellationToken ct)
    {
        if (_cache.TryGetValue(CacheKey(playfieldId), out IReadOnlyList<GpsCoordinate>? cached) && cached is not null)
            return cached;

        await using var scope = _scopeFactory.CreateAsyncScope();
        var provider = scope.ServiceProvider.GetRequiredService<IPlayfieldInfoProvider>();
        var info = await provider.GetAsync(playfieldId, ct);

        var polygon = info is null
            ? Empty
            : info.Coordinates.Select(c => GpsCoordinate.Create(c.Latitude, c.Longitude)).ToList();

        // Only cache a real polygon (immutable per playfield). A missing/failed lookup is left uncached
        // so it can be retried on a later tick.
        if (polygon.Count > 0)
            _cache.Set(CacheKey(playfieldId), polygon);

        return polygon;
    }

    private static string CacheKey(Guid playfieldId) => $"theprey:games:playfield-polygon:{playfieldId}";
}
