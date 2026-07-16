using HexMaster.ThePrey.Maui.App.Services.Location;

namespace HexMaster.ThePrey.Maui.App.Tests;

public class GpsCoordinateFormatterTests
{
    [Fact]
    public void Format_ShouldZeroPadDegreesWithNorthEast_WhenPositiveCoordinates()
    {
        var result = GpsCoordinateFormatter.Format(52.1, 4.9);

        Assert.Equal("052° N // 005° E", result);
    }

    [Fact]
    public void Format_ShouldUseSouthAndWestHemispheres_WhenNegativeCoordinates()
    {
        var result = GpsCoordinateFormatter.Format(-33.4, -70.6);

        Assert.Equal("033° S // 071° W", result);
    }

    [Fact]
    public void Format_ShouldPadThreeDigitLongitude_WhenNearAntimeridian()
    {
        var result = GpsCoordinateFormatter.Format(1.0, 151.0);

        Assert.Equal("001° N // 151° E", result);
    }

    [Fact]
    public void Format_ShouldReturnPlaceholder_WhenFixIsNull()
    {
        var result = GpsCoordinateFormatter.Format((GpsFix?)null);

        Assert.Equal(GpsCoordinateFormatter.Placeholder, result);
    }

    [Fact]
    public void Format_ShouldUseFixValues_WhenFixProvided()
    {
        var result = GpsCoordinateFormatter.Format(new GpsFix(0.2, 0.8));

        Assert.Equal("000° N // 001° E", result);
    }
}
