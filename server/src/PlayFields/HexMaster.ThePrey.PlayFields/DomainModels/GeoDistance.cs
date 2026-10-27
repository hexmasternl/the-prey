namespace HexMaster.ThePrey.PlayFields.DomainModels;

/// <summary>Great-circle distances between <see cref="GpsCoordinate"/>s.</summary>
public static class GeoDistance
{
    private const double EarthRadiusMeters = 6_371_000;

    /// <summary>The haversine distance between <paramref name="a"/> and <paramref name="b"/>, in metres.</summary>
    public static double Meters(GpsCoordinate a, GpsCoordinate b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);

        var dLat = ToRadians(b.Latitude - a.Latitude);
        var dLon = ToRadians(b.Longitude - a.Longitude);
        var h = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
              + Math.Cos(ToRadians(a.Latitude)) * Math.Cos(ToRadians(b.Latitude))
              * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return 2 * EarthRadiusMeters * Math.Asin(Math.Min(1, Math.Sqrt(h)));
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180;
}
