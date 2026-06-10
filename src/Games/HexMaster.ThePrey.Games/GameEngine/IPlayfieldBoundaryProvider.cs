using HexMaster.ThePrey.Games.DomainModels;

namespace HexMaster.ThePrey.Games.GameEngine;

/// <summary>Supplies a game's playfield boundary polygon, caching it because it is immutable per game.</summary>
public interface IPlayfieldBoundaryProvider
{
    /// <summary>
    /// Returns the playfield's boundary polygon as an ordered list of vertices, or an empty list
    /// when the playfield is unknown / could not be loaded (callers then skip boundary checks).
    /// </summary>
    Task<IReadOnlyList<GpsCoordinate>> GetPolygonAsync(Guid playfieldId, CancellationToken ct);
}
