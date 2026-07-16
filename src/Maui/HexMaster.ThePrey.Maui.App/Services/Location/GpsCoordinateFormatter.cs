namespace HexMaster.ThePrey.Maui.App.Services.Location;

/// <summary>
/// Formats a coordinate into the tactical HUD readout — coarse whole degrees with a hemisphere
/// suffix, e.g. <c>052° N // 004° E</c>. Coarse on purpose: the readout is decorative flavor and
/// must not broadcast a precise location. Returns a neutral placeholder when there is no fix.
/// </summary>
public static class GpsCoordinateFormatter
{
    public const string Placeholder = "---° N // ---° E";

    public static string Format(GpsFix? fix) =>
        fix is null ? Placeholder : Format(fix.Latitude, fix.Longitude);

    public static string Format(double latitude, double longitude)
    {
        var latHemisphere = latitude >= 0 ? "N" : "S";
        var lonHemisphere = longitude >= 0 ? "E" : "W";
        var latDegrees = (int)Math.Round(Math.Abs(latitude));
        var lonDegrees = (int)Math.Round(Math.Abs(longitude));
        return $"{latDegrees:000}° {latHemisphere} // {lonDegrees:000}° {lonHemisphere}";
    }
}
