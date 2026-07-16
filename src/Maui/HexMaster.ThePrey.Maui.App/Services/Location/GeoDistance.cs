namespace HexMaster.ThePrey.Maui.App.Services.Location;

/// <summary>
/// Great-circle distance helper. Used by the hunter's HUD to compute the distance from the device fix
/// to the nearest prey location the server reports. Plain .NET so it stays unit-testable.
/// </summary>
public static class GeoDistance
{
    /// <summary>Mean Earth radius in metres (WGS-84 authalic sphere).</summary>
    private const double EarthRadiusMeters = 6_371_000d;

    /// <summary>
    /// The haversine distance in metres between two latitude/longitude points (degrees). The result is
    /// always non-negative and is symmetric in its arguments.
    /// </summary>
    public static double Haversine(double lat1, double lon1, double lat2, double lon2)
    {
        var phi1 = DegreesToRadians(lat1);
        var phi2 = DegreesToRadians(lat2);
        var deltaPhi = DegreesToRadians(lat2 - lat1);
        var deltaLambda = DegreesToRadians(lon2 - lon1);

        var a = Math.Sin(deltaPhi / 2) * Math.Sin(deltaPhi / 2)
            + Math.Cos(phi1) * Math.Cos(phi2) * Math.Sin(deltaLambda / 2) * Math.Sin(deltaLambda / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

        return EarthRadiusMeters * c;
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180d;
}
