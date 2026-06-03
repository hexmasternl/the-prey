using HexMaster.ThePrey.PlayFields.DomainModels;
using HexMaster.ThePrey.PlayFields.Features.ListPlayFields;
using HexMaster.ThePrey.PlayFields.Tests.Factories;
using Moq;

namespace HexMaster.ThePrey.PlayFields.Tests.ListPlayFields;

public sealed class ListPlayFieldsQueryHandlerTests
{
    private readonly Mock<IPlayFieldRepository> _mockRepository;
    private readonly ListPlayFieldsQueryHandler _handler;

    public ListPlayFieldsQueryHandlerTests()
    {
        _mockRepository = new Mock<IPlayFieldRepository>();
        _handler = new ListPlayFieldsQueryHandler(_mockRepository.Object);
    }

    [Fact]
    public async Task Handle_ShouldReturnSummariesForVisibleFields()
    {
        var owned = PlayFieldFaker.CreateValid(ownerId: "auth0|owner", isPublic: false);
        var othersPublic = PlayFieldFaker.CreateValid(ownerId: "auth0|other", isPublic: true);

        // The repository is responsible for the visibility filter; the handler maps what it returns.
        _mockRepository
            .Setup(r => r.ListVisibleToAsync("auth0|owner", It.IsAny<CancellationToken>()))
            .ReturnsAsync([owned, othersPublic]);

        var result = await _handler.Handle(new ListPlayFieldsQuery("auth0|owner"), CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, s => s.Id == owned.Id);
        Assert.Contains(result, s => s.Id == othersPublic.Id);
    }

    [Fact]
    public async Task Handle_ShouldReturnEmpty_WhenNoVisibleFields()
    {
        _mockRepository
            .Setup(r => r.ListVisibleToAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<PlayField>());

        var result = await _handler.Handle(new ListPlayFieldsQuery("auth0|owner"), CancellationToken.None);

        Assert.Empty(result);
    }
}
