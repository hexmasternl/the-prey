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

    // ─── Centroid ────────────────────────────────────────────────────────────

    [Fact]
    public void Create_ShouldComputeCentroid_AsMeanOfVertices()
    {
        // Square: (0,0) (0,2) (2,2) (2,0) — mean lat=1, lon=1
        var points = new[]
        {
            GpsCoordinate.Create(0, 0),
            GpsCoordinate.Create(0, 2),
            GpsCoordinate.Create(2, 2),
            GpsCoordinate.Create(2, 0)
        };

        var playField = PlayField.Create("Park", "auth0|owner", points, false);

        Assert.NotNull(playField.CenterCoordinates);
        Assert.Equal(1.0, playField.CenterCoordinates!.Latitude, precision: 10);
        Assert.Equal(1.0, playField.CenterCoordinates.Longitude, precision: 10);
    }

    [Fact]
    public void Create_ShouldStampLastModifiedOn_WithUtcNow_WhenNotSupplied()
    {
        var before = DateTimeOffset.UtcNow;
        var playField = PlayFieldFaker.CreateValid();
        var after = DateTimeOffset.UtcNow;

        Assert.InRange(playField.LastModifiedOn, before, after);
    }

    [Fact]
    public void Create_ShouldAcceptSuppliedIdAndTimestamp()
    {
        var id = Guid.NewGuid();
        var ts = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);

        var playField = PlayField.Create("Park", "auth0|owner", PlayFieldFaker.SquarePoints(), false, id, ts);

        Assert.Equal(id, playField.Id);
        Assert.Equal(ts, playField.LastModifiedOn);
    }

    // ─── Update ──────────────────────────────────────────────────────────────

    [Fact]
    public void Update_ShouldApplyChanges_AndRecomputeCentroid()
    {
        var playField = PlayFieldFaker.CreateValid();
        var newPoints = new[]
        {
            GpsCoordinate.Create(0, 0),
            GpsCoordinate.Create(0, 4),
            GpsCoordinate.Create(4, 4),
            GpsCoordinate.Create(4, 0)
        };
        var ts = DateTimeOffset.UtcNow;

        playField.Update("New Name", true, newPoints, ts);

        Assert.Equal("New Name", playField.Name);
        Assert.True(playField.IsPublic);
        Assert.Equal(ts, playField.LastModifiedOn);
        Assert.Equal(4, playField.Points.Count);
        Assert.NotNull(playField.CenterCoordinates);
        Assert.Equal(2.0, playField.CenterCoordinates!.Latitude, precision: 10);
        Assert.Equal(2.0, playField.CenterCoordinates.Longitude, precision: 10);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Update_ShouldThrow_WhenNameIsBlank(string name)
    {
        var playField = PlayFieldFaker.CreateValid();

        Assert.Throws<ArgumentException>(() =>
            playField.Update(name, false, PlayFieldFaker.SquarePoints(), DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Update_ShouldThrow_WhenFewerThanThreePoints()
    {
        var playField = PlayFieldFaker.CreateValid();
        var twoPoints = new[] { GpsCoordinate.Create(1, 1), GpsCoordinate.Create(2, 2) };

        Assert.Throws<ArgumentException>(() =>
            playField.Update("Valid", false, twoPoints, DateTimeOffset.UtcNow));
    }
}
