using HexMaster.ThePrey.PlayFields.DomainModels;

namespace HexMaster.ThePrey.PlayFields.Tests.DomainModels;

public sealed class GeoDistanceTests
{
    [Fact]
    public void Meters_ShouldBeAbout35Kilometres_WhenAmsterdamToUtrecht()
    {
        var amsterdam = GpsCoordinate.Create(52.3731, 4.8922);
        var utrecht = GpsCoordinate.Create(52.0907, 5.1214);

        var meters = GeoDistance.Meters(amsterdam, utrecht);

        Assert.InRange(meters, 34_000, 36_000);
    }

    [Fact]
    public void Meters_ShouldBeZero_WhenBothPointsAreTheSame()
    {
        var point = GpsCoordinate.Create(52.0, 5.0);

        Assert.Equal(0, GeoDistance.Meters(point, point));
    }

    [Fact]
    public void Meters_ShouldBeSymmetric()
    {
        var a = GpsCoordinate.Create(52.0, 5.0);
        var b = GpsCoordinate.Create(52.1, 5.2);

        Assert.Equal(GeoDistance.Meters(a, b), GeoDistance.Meters(b, a), 6);
    }
}
