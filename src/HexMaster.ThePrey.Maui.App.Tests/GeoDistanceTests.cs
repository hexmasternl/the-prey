using HexMaster.ThePrey.Maui.App.Services.Location;

namespace HexMaster.ThePrey.Maui.App.Tests;

public class GeoDistanceTests
{
    [Fact]
    public void Haversine_ShouldReturnZero_WhenPointsAreIdentical()
    {
        var d = GeoDistance.Haversine(52.3676, 4.9041, 52.3676, 4.9041);

        Assert.Equal(0d, d, 3);
    }

    [Fact]
    public void Haversine_ShouldReturnAboutOneDegreeOfLatitude_InMeters()
    {
        // One degree of latitude is ~111.19 km anywhere on the globe.
        var d = GeoDistance.Haversine(0d, 0d, 1d, 0d);

        Assert.InRange(d, 111_000d, 111_400d);
    }

    [Fact]
    public void Haversine_ShouldMatchKnownCityDistance_AmsterdamToRotterdam()
    {
        // Amsterdam (52.3676, 4.9041) → Rotterdam (51.9225, 4.4792): ~57.6 km great-circle.
        var d = GeoDistance.Haversine(52.3676, 4.9041, 51.9225, 4.4792);

        Assert.InRange(d, 56_500d, 58_500d);
    }

    [Fact]
    public void Haversine_ShouldBeSymmetric()
    {
        var forward = GeoDistance.Haversine(52.0, 4.0, 48.85, 2.35);
        var backward = GeoDistance.Haversine(48.85, 2.35, 52.0, 4.0);

        Assert.Equal(forward, backward, 6);
    }
}
