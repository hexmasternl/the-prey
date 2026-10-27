using HexMaster.ThePrey.PlayFields.Features.SearchPublicPlayFields;

namespace HexMaster.ThePrey.PlayFields.Tests.SearchPublicPlayFields;

/// <summary>The HTTP parameter rules of <c>GET /playfields/public</c> (the endpoint maps the errors to a 400).</summary>
public sealed class SearchPublicPlayFieldsQueryTests
{
    [Fact]
    public void FromRequest_ShouldBuildNameSearch_WhenOnlyNameIsGiven()
    {
        var query = SearchPublicPlayFieldsQuery.FromRequest(" vondel ", null, null, null, out var errors);

        Assert.Empty(errors);
        Assert.NotNull(query);
        Assert.Equal("vondel", query.SearchText);
        Assert.Null(query.Origin);
    }

    [Fact]
    public void FromRequest_ShouldBuildNearSearchWithDefaultRadius_WhenOnlyPositionIsGiven()
    {
        var query = SearchPublicPlayFieldsQuery.FromRequest(null, 52.36, 4.87, null, out var errors);

        Assert.Empty(errors);
        Assert.NotNull(query);
        Assert.Null(query.SearchText);
        Assert.Equal(52.36, query.Origin!.Latitude);
        Assert.Equal(4.87, query.Origin.Longitude);
        Assert.Equal(SearchPublicPlayFieldsQuery.DefaultRadiusKm, query.RadiusKm);
    }

    [Fact]
    public void FromRequest_ShouldBuildCombinedSearch_WhenNameAndPositionAreGiven()
    {
        var query = SearchPublicPlayFieldsQuery.FromRequest("park", 52.36, 4.87, 500, out var errors);

        Assert.Empty(errors);
        Assert.Equal("park", query!.SearchText);
        Assert.NotNull(query.Origin);
        // Clamped by the handler, not rejected here.
        Assert.Equal(500, query.RadiusKm);
    }

    [Theory]
    [InlineData(" ")]
    [InlineData("")]
    public void FromRequest_ShouldIgnoreBlankName_WhenPositionIsGiven(string q)
    {
        var query = SearchPublicPlayFieldsQuery.FromRequest(q, 52.36, 4.87, null, out var errors);

        Assert.Empty(errors);
        Assert.Null(query!.SearchText);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(" ")]
    [InlineData("vo")]
    public void FromRequest_ShouldRejectQ_WhenNameIsMissingOrTooShortWithoutPosition(string? q)
    {
        var query = SearchPublicPlayFieldsQuery.FromRequest(q, null, null, null, out var errors);

        Assert.Null(query);
        Assert.Equal(["q"], errors.Keys);
    }

    [Fact]
    public void FromRequest_ShouldRejectQ_WhenNameIsTooShortWithPosition()
    {
        var query = SearchPublicPlayFieldsQuery.FromRequest("vo", 52.36, 4.87, null, out var errors);

        Assert.Null(query);
        Assert.Equal(["q"], errors.Keys);
    }

    [Fact]
    public void FromRequest_ShouldRejectLon_WhenOnlyLatIsGiven()
    {
        var query = SearchPublicPlayFieldsQuery.FromRequest(null, 52.36, null, null, out var errors);

        Assert.Null(query);
        Assert.Equal(["lon"], errors.Keys);
    }

    [Fact]
    public void FromRequest_ShouldRejectLat_WhenOnlyLonIsGiven()
    {
        var query = SearchPublicPlayFieldsQuery.FromRequest("park", null, 4.87, null, out var errors);

        Assert.Null(query);
        Assert.Equal(["lat"], errors.Keys);
    }

    [Theory]
    [InlineData(95, 4.87, "lat")]
    [InlineData(-90.5, 4.87, "lat")]
    [InlineData(double.NaN, 4.87, "lat")]
    [InlineData(52.36, 181, "lon")]
    [InlineData(52.36, -180.5, "lon")]
    public void FromRequest_ShouldRejectTheCoordinate_WhenItIsOutOfRange(double lat, double lon, string key)
    {
        var query = SearchPublicPlayFieldsQuery.FromRequest(null, lat, lon, null, out var errors);

        Assert.Null(query);
        Assert.Equal([key], errors.Keys);
    }
}
