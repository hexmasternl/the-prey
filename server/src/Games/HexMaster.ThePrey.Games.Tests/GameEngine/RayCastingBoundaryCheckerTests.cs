using HexMaster.ThePrey.Games.DomainModels;
using HexMaster.ThePrey.Games.GameEngine;

namespace HexMaster.ThePrey.Games.Tests.GameEngine;

public sealed class RayCastingBoundaryCheckerTests
{
    private readonly RayCastingBoundaryChecker _sut = new();

    // A simple 2x2 square centred on the origin: (1,1) (1,-1) (-1,-1) (-1,1) as (lat, lng).
    private static readonly IReadOnlyList<GpsCoordinate> Square =
    [
        GpsCoordinate.Create(1, 1),
        GpsCoordinate.Create(1, -1),
        GpsCoordinate.Create(-1, -1),
        GpsCoordinate.Create(-1, 1),
    ];

    [Fact]
    public void IsInside_ShouldReturnTrue_WhenPointIsInsideSquare()
        => Assert.True(_sut.IsInside(Square, GpsCoordinate.Create(0, 0)));

    [Fact]
    public void IsInside_ShouldReturnFalse_WhenPointIsOutsideSquare()
        => Assert.False(_sut.IsInside(Square, GpsCoordinate.Create(5, 5)));

    [Fact]
    public void IsInside_ShouldReturnTrue_WhenPolygonHasFewerThanThreeVertices()
        => Assert.True(_sut.IsInside([GpsCoordinate.Create(0, 0), GpsCoordinate.Create(1, 1)], GpsCoordinate.Create(50, 50)));

    [Theory]
    [InlineData(3, 2.5, false)]    // inside the notch (the concave bite) → outside the polygon
    [InlineData(0.5, 0.5, true)]   // solid part of the polygon → inside
    public void IsInside_ShouldHandleConcavePolygon(double lat, double lng, bool expected)
    {
        // In (x=lng, y=lat) space: a 4x4 square with a rectangular notch removed at x in [2,3],
        // y in [2,4], making it concave. So (x=2.5, y=3) sits in the notch (outside).
        IReadOnlyList<GpsCoordinate> concave =
        [
            GpsCoordinate.Create(0, 0),
            GpsCoordinate.Create(0, 4),
            GpsCoordinate.Create(4, 4),
            GpsCoordinate.Create(4, 3),
            GpsCoordinate.Create(2, 3),
            GpsCoordinate.Create(2, 2),
            GpsCoordinate.Create(4, 2),
            GpsCoordinate.Create(4, 0),
        ];

        Assert.Equal(expected, _sut.IsInside(concave, GpsCoordinate.Create(lat, lng)));
    }
}
