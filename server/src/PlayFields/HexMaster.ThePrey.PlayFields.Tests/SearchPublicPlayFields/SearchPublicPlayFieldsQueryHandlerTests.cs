using HexMaster.ThePrey.PlayFields.DomainModels;
using HexMaster.ThePrey.PlayFields.Features.SearchPublicPlayFields;
using HexMaster.ThePrey.PlayFields.Observability;
using HexMaster.ThePrey.PlayFields.Tests.Factories;
using Microsoft.Extensions.Logging;
using Moq;

namespace HexMaster.ThePrey.PlayFields.Tests.SearchPublicPlayFields;

public sealed class SearchPublicPlayFieldsQueryHandlerTests
{
    private readonly Mock<IPlayFieldRepository> _mockRepository;
    private readonly Mock<IPlayFieldMetrics> _mockMetrics;
    private readonly SearchPublicPlayFieldsQueryHandler _handler;

    public SearchPublicPlayFieldsQueryHandlerTests()
    {
        _mockRepository = new Mock<IPlayFieldRepository>();
        _mockMetrics = new Mock<IPlayFieldMetrics>();
        _handler = new SearchPublicPlayFieldsQueryHandler(
            _mockRepository.Object,
            _mockMetrics.Object,
            Mock.Of<ILogger<SearchPublicPlayFieldsQueryHandler>>());
    }

    [Fact]
    public async Task Handle_ShouldReturnSummaries_WhenRepositoryFindsMatches()
    {
        var first = PlayFieldFaker.CreateValid(name: "Central Park", isPublic: true);
        var second = PlayFieldFaker.CreateValid(name: "Park South", isPublic: true);

        // The repository owns the public-only, name-contains filter; the handler maps its results.
        _mockRepository
            .Setup(r => r.SearchPublicAsync("park", It.IsAny<CancellationToken>()))
            .ReturnsAsync([first, second]);

        var result = await _handler.Handle(new SearchPublicPlayFieldsQuery("park"), CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, s => s.Id == first.Id);
        Assert.Contains(result, s => s.Id == second.Id);
        _mockRepository.Verify(r => r.SearchPublicAsync("park", It.IsAny<CancellationToken>()), Times.Once);
        _mockMetrics.Verify(m => m.RecordPublicPlayFieldSearch(), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldTrimSearchText_BeforeQueryingRepository()
    {
        _mockRepository
            .Setup(r => r.SearchPublicAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<PlayField>());

        await _handler.Handle(new SearchPublicPlayFieldsQuery("  park  "), CancellationToken.None);

        _mockRepository.Verify(r => r.SearchPublicAsync("park", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldReturnEmpty_WhenNothingMatches()
    {
        _mockRepository
            .Setup(r => r.SearchPublicAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<PlayField>());

        var result = await _handler.Handle(new SearchPublicPlayFieldsQuery("nomatch"), CancellationToken.None);

        Assert.Empty(result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("ab")]
    [InlineData(" ab ")]
    public async Task Handle_ShouldThrowAndNotSearch_WhenSearchTextIsTooShort(string? searchText)
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => _handler.Handle(new SearchPublicPlayFieldsQuery(searchText!), CancellationToken.None));

        _mockRepository.Verify(r => r.SearchPublicAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockMetrics.Verify(m => m.RecordPublicPlayFieldSearch(), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenQueryIsNull()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _handler.Handle(null!, CancellationToken.None));
    }

    // ---- near-me ----

    private const double OriginLat = 52.36;
    private const double OriginLon = 4.87;
    private static readonly GpsCoordinate Origin = GpsCoordinate.Create(OriginLat, OriginLon);

    private static PlayField NorthOfOrigin(double kilometres, string? name = null) =>
        PlayFieldFaker.CreatePublicNorthOf(OriginLat, OriginLon, kilometres, name);

    private void RepositoryReturns(string? searchText, params PlayField[] playFields) =>
        _mockRepository
            .Setup(r => r.SearchPublicAsync(searchText, It.IsAny<CancellationToken>()))
            .ReturnsAsync(playFields);

    [Fact]
    public async Task Handle_ShouldLeaveDistanceNull_WhenSearchingByNameOnly()
    {
        RepositoryReturns("park", NorthOfOrigin(2));

        var result = await _handler.Handle(new SearchPublicPlayFieldsQuery("park"), CancellationToken.None);

        Assert.Null(Assert.Single(result).DistanceMeters);
    }

    [Fact]
    public async Task Handle_ShouldReturnNearestFirstWithinRadius_WhenSearchingNearAPosition()
    {
        var far = NorthOfOrigin(40);
        var mid = NorthOfOrigin(8);
        var near = NorthOfOrigin(1.2);
        RepositoryReturns(null, far, mid, near);

        var result = await _handler.Handle(new SearchPublicPlayFieldsQuery(null, Origin), CancellationToken.None);

        Assert.Equal([near.Id, mid.Id], result.Select(r => r.Id));
        Assert.InRange(result[0].DistanceMeters!.Value, 1_190, 1_210);
        Assert.InRange(result[1].DistanceMeters!.Value, 7_990, 8_010);
    }

    [Fact]
    public async Task Handle_ShouldFilterByNameAndDistance_WhenBothAreGiven()
    {
        var inRange = NorthOfOrigin(3, "NL, Amsterdam, Park East");
        var outOfRange = NorthOfOrigin(30, "NL, Haarlem, Park West");
        RepositoryReturns("park", outOfRange, inRange);

        var result = await _handler.Handle(new SearchPublicPlayFieldsQuery(" park ", Origin), CancellationToken.None);

        Assert.Equal(inRange.Id, Assert.Single(result).Id);
        _mockRepository.Verify(r => r.SearchPublicAsync("park", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task Handle_ShouldSearchWithoutNameFilter_WhenNameIsBlankAndPositionIsGiven(string? searchText)
    {
        RepositoryReturns(null, NorthOfOrigin(1));

        var result = await _handler.Handle(new SearchPublicPlayFieldsQuery(searchText, Origin), CancellationToken.None);

        Assert.Single(result);
        _mockRepository.Verify(r => r.SearchPublicAsync(null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldLeaveOutPlayFieldsWithoutCentre_WhenSearchingNearAPosition()
    {
        var withCentre = NorthOfOrigin(1);
        RepositoryReturns(null, PlayFieldFaker.CreatePublicWithoutCenter(), withCentre);

        var result = await _handler.Handle(new SearchPublicPlayFieldsQuery(null, Origin), CancellationToken.None);

        Assert.Equal(withCentre.Id, Assert.Single(result).Id);
    }

    [Fact]
    public async Task Handle_ShouldUseTwentyFiveKilometres_WhenNoRadiusIsGiven()
    {
        var inside = NorthOfOrigin(24.9);
        var outside = NorthOfOrigin(25.1);
        RepositoryReturns(null, inside, outside);

        var result = await _handler.Handle(new SearchPublicPlayFieldsQuery(null, Origin), CancellationToken.None);

        Assert.Equal(inside.Id, Assert.Single(result).Id);
    }

    [Theory]
    [InlineData(500, 99, 101)]
    [InlineData(0.1, 0.9, 1.1)]
    [InlineData(double.NaN, 24.9, 25.1)]
    public async Task Handle_ShouldClampTheRadius_WhenItIsOutOfRange(double radiusKm, double insideKm, double outsideKm)
    {
        var inside = NorthOfOrigin(insideKm);
        var outside = NorthOfOrigin(outsideKm);
        RepositoryReturns(null, inside, outside);

        var result = await _handler.Handle(new SearchPublicPlayFieldsQuery(null, Origin, radiusKm), CancellationToken.None);

        Assert.Equal(inside.Id, Assert.Single(result).Id);
    }

    [Fact]
    public async Task Handle_ShouldReturnTheFiftyNearest_WhenMoreAreInRange()
    {
        // Nearest first in this list; the repository hands them over farthest first.
        var nearestFirst = Enumerable.Range(1, 60).Select(i => NorthOfOrigin(i * 0.3)).ToList();
        RepositoryReturns(null, Enumerable.Reverse(nearestFirst).ToArray());

        var result = await _handler.Handle(new SearchPublicPlayFieldsQuery(null, Origin), CancellationToken.None);

        Assert.Equal(SearchPublicPlayFieldsQuery.MaximumNearbyResults, result.Count);
        Assert.Equal(nearestFirst.Take(50).Select(p => p.Id), result.Select(r => r.Id));
    }

    [Theory]
    [InlineData(90.1, 4.87)]
    [InlineData(-90.1, 4.87)]
    [InlineData(52.36, 180.1)]
    [InlineData(52.36, -180.1)]
    public async Task Handle_ShouldThrowAndNotSearch_WhenPositionIsOutOfRange(double latitude, double longitude)
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _handler.Handle(new SearchPublicPlayFieldsQuery(null, new GpsCoordinate(latitude, longitude)), CancellationToken.None));

        _mockRepository.Verify(r => r.SearchPublicAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldThrowAndNotSearch_WhenNameIsTooShortWithAPosition()
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => _handler.Handle(new SearchPublicPlayFieldsQuery("ab", Origin), CancellationToken.None));

        _mockRepository.Verify(r => r.SearchPublicAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
