using HexMaster.ThePrey.PlayFields.DomainModels;
using HexMaster.ThePrey.PlayFields.Tests.Factories;

namespace HexMaster.ThePrey.PlayFields.Tests.DomainModels;

public sealed class PlayFieldTests
{
    [Fact]
    public void Create_ShouldSucceed_WhenInputIsValid()
    {
        var playField = PlayField.Create("Vondelpark", "auth0|owner", PlayFieldFaker.SquarePoints(), isPublic: true);

        Assert.NotEqual(Guid.Empty, playField.Id);
        Assert.Equal("Vondelpark", playField.Name);
        Assert.Equal("auth0|owner", playField.OwnerId);
        Assert.True(playField.IsPublic);
        Assert.Equal(4, playField.Points.Count);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_ShouldThrow_WhenNameIsBlank(string name)
    {
        Assert.Throws<ArgumentException>(() =>
            PlayField.Create(name, "auth0|owner", PlayFieldFaker.SquarePoints(), false));
    }

    [Fact]
    public void Create_ShouldThrow_WhenOwnerIsBlank()
    {
        Assert.Throws<ArgumentException>(() =>
            PlayField.Create("Field", "", PlayFieldFaker.SquarePoints(), false));
    }

    [Fact]
    public void Create_ShouldThrow_WhenFewerThanThreePoints()
    {
        var points = new[]
        {
            GpsCoordinate.Create(52.0, 5.0),
            GpsCoordinate.Create(52.0, 5.1)
        };

        Assert.Throws<ArgumentException>(() => PlayField.Create("Field", "auth0|owner", points, false));
    }

    [Fact]
    public void IsInPlayfield_ShouldReturnTrue_WhenCoordinateInside()
    {
        var playField = PlayField.Create("Field", "auth0|owner", PlayFieldFaker.SquarePoints(52.0, 5.0, 0.01), false);

        var inside = GpsCoordinate.Create(52.005, 5.005);

        Assert.True(playField.IsInPlayfield(inside));
    }

    [Fact]
    public void IsInPlayfield_ShouldReturnFalse_WhenCoordinateOutside()
    {
        var playField = PlayField.Create("Field", "auth0|owner", PlayFieldFaker.SquarePoints(52.0, 5.0, 0.01), false);

        var outside = GpsCoordinate.Create(52.5, 5.5);

        Assert.False(playField.IsInPlayfield(outside));
    }

    [Fact]
    public void IsInPlayfield_ShouldReturnFalse_ForPointInConcaveNotch()
    {
        // An L-shaped (concave) polygon. Vertices given as (x=longitude, y=latitude) via Create(lat, lon):
        //   (0,0) (2,0) (2,1) (1,1) (1,2) (0,2)
        // The square region x in [1,2], y in [1,2] is the concave notch — OUTSIDE the polygon.
        var points = new[]
        {
            GpsCoordinate.Create(0, 0),
            GpsCoordinate.Create(0, 2),
            GpsCoordinate.Create(1, 2),
            GpsCoordinate.Create(1, 1),
            GpsCoordinate.Create(2, 1),
            GpsCoordinate.Create(2, 0)
        };
        var playField = PlayField.Create("Concave", "auth0|owner", points, false);

        // Point in the notch -> outside the polygon.
        Assert.False(playField.IsInPlayfield(GpsCoordinate.Create(1.5, 1.5)));

        // Points in the solid arms -> inside.
        Assert.True(playField.IsInPlayfield(GpsCoordinate.Create(0.5, 0.5)));
        Assert.True(playField.IsInPlayfield(GpsCoordinate.Create(1.5, 0.5)));
    }
}
