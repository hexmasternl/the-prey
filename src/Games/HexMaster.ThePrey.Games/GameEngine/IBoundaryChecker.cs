using HexMaster.ThePrey.Games.DomainModels;

namespace HexMaster.ThePrey.Games.GameEngine;

/// <summary>Tests whether a GPS coordinate falls inside a playfield polygon.</summary>
public interface IBoundaryChecker
{
    /// <summary>
    /// Returns true when <paramref name="point"/> is inside the closed polygon described by
    /// <paramref name="polygon"/> (a list of vertices in order). A polygon with fewer than three
    /// vertices is treated as having no boundary, so every point is considered inside.
    /// </summary>
    bool IsInside(IReadOnlyList<GpsCoordinate> polygon, GpsCoordinate point);
}
