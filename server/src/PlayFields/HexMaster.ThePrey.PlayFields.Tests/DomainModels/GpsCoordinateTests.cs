using HexMaster.ThePrey.PlayFields.DomainModels;

namespace HexMaster.ThePrey.PlayFields.Tests.DomainModels;

public sealed class GpsCoordinateTests
{
    [Fact]
    public void Create_ShouldReturnCoordinate_WhenValuesAreInRange()
    {
        var coordinate = GpsCoordinate.Create(52.379189, 4.899431);

        Assert.Equal(52.379189, coordinate.Latitude);
        Assert.Equal(4.899431, coordinate.Longitude);
    }

    [Theory]
    [InlineData(-90)]
    [InlineData(90)]
    [InlineData(0)]
    public void Create_ShouldAcceptBoundaryLatitudes(double latitude)
    {
        var coordinate = GpsCoordinate.Create(latitude, 0);
        Assert.Equal(latitude, coordinate.Latitude);
    }

    [Theory]
    [InlineData(-90.1)]
    [InlineData(90.1)]
    public void Create_ShouldThrow_WhenLatitudeOutOfRange(double latitude)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => GpsCoordinate.Create(latitude, 0));
    }

    [Theory]
    [InlineData(-180.1)]
    [InlineData(180.1)]
    public void Create_ShouldThrow_WhenLongitudeOutOfRange(double longitude)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => GpsCoordinate.Create(0, longitude));
    }
}
