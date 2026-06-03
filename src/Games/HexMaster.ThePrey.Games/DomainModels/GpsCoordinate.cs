namespace HexMaster.ThePrey.Games.DomainModels;

/// <summary>
/// A WGS84 GPS coordinate. Use <see cref="Create"/> to obtain a validated instance.
/// </summary>
/// <remarks>
/// Defined locally to keep the Games module self-contained; a near-identical type exists in the
/// PlayFields module. A future refactor may hoist a shared geo value object into a shared kernel.
/// </remarks>
public sealed record GpsCoordinate(double Latitude, double Longitude)
{
    public static GpsCoordinate Create(double latitude, double longitude)
    {
        if (latitude is < -90 or > 90)
            throw new ArgumentOutOfRangeException(nameof(latitude), latitude, "Latitude must be between -90 and 90 degrees.");

        if (longitude is < -180 or > 180)
            throw new ArgumentOutOfRangeException(nameof(longitude), longitude, "Longitude must be between -180 and 180 degrees.");

        return new GpsCoordinate(latitude, longitude);
    }
}
