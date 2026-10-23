using HexMaster.ThePrey.Games.DomainModels;

namespace HexMaster.ThePrey.Games.GameEngine;

/// <summary>
/// Point-in-polygon test using the ray-casting (even-odd rule) algorithm. Longitude is treated as X
/// and latitude as Y. Ported from the PlayFields module so the Games module stays independent (modules
/// do not share domain types). Pure and allocation-free per call.
/// </summary>
public sealed class RayCastingBoundaryChecker : IBoundaryChecker
{
    private const int MinimumPolygonVertices = 3;

    public bool IsInside(IReadOnlyList<GpsCoordinate> polygon, GpsCoordinate point)
    {
        ArgumentNullException.ThrowIfNull(polygon);
        ArgumentNullException.ThrowIfNull(point);

        // Without a real polygon we cannot decide a boundary; treat everything as inside so we never
        // penalise a player for a missing/degenerate playfield.
        if (polygon.Count < MinimumPolygonVertices)
            return true;

        var inside = false;
        var x = point.Longitude;
        var y = point.Latitude;

        for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
        {
            var xi = polygon[i].Longitude;
            var yi = polygon[i].Latitude;
            var xj = polygon[j].Longitude;
            var yj = polygon[j].Latitude;

            var intersects = (yi > y) != (yj > y)
                && x < ((xj - xi) * (y - yi) / (yj - yi)) + xi;

            if (intersects)
                inside = !inside;
        }

        return inside;
    }
}
