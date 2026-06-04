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
}
