using HexMaster.ThePrey.PlayFields.DomainModels;
using HexMaster.ThePrey.PlayFields.Features.ListPlayFields;
using HexMaster.ThePrey.PlayFields.Tests.Factories;
using Moq;

namespace HexMaster.ThePrey.PlayFields.Tests.ListPlayFields;

public sealed class ListPlayFieldsQueryHandlerTests
{
    private static readonly Guid OwnerGuid = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherGuid = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private readonly Mock<IPlayFieldRepository> _mockRepository;
    private readonly ListPlayFieldsQueryHandler _handler;

    public ListPlayFieldsQueryHandlerTests()
    {
        _mockRepository = new Mock<IPlayFieldRepository>();
        _handler = new ListPlayFieldsQueryHandler(_mockRepository.Object);
    }

    [Fact]
    public async Task Handle_ShouldReturnSummaries_WhenOwnerHasPlayFields()
    {
        var owned1 = PlayFieldFaker.CreateValid(ownerId: OwnerGuid, isPublic: false);
        var owned2 = PlayFieldFaker.CreateValid(ownerId: OwnerGuid, isPublic: true);

        _mockRepository
            .Setup(r => r.ListByOwnerAsync(OwnerGuid, It.IsAny<CancellationToken>()))
            .ReturnsAsync([owned1, owned2]);

        var result = await _handler.Handle(new ListPlayFieldsQuery(OwnerGuid), CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, s => s.Id == owned1.Id);
        Assert.Contains(result, s => s.Id == owned2.Id);
    }

    [Fact]
    public async Task Handle_ShouldNotReturnOtherOwnersPlayFields()
    {
        var owned = PlayFieldFaker.CreateValid(ownerId: OwnerGuid, isPublic: false);

        _mockRepository
            .Setup(r => r.ListByOwnerAsync(OwnerGuid, It.IsAny<CancellationToken>()))
            .ReturnsAsync([owned]);

        var result = await _handler.Handle(new ListPlayFieldsQuery(OwnerGuid), CancellationToken.None);

        Assert.Single(result);
        Assert.All(result, s => Assert.Equal(OwnerGuid, s.OwnerId));
    }

    [Fact]
    public async Task Handle_ShouldReturnEmpty_WhenOwnerHasNoPlayFields()
    {
        _mockRepository
            .Setup(r => r.ListByOwnerAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<PlayField>());

        var result = await _handler.Handle(new ListPlayFieldsQuery(OwnerGuid), CancellationToken.None);

        Assert.Empty(result);
    }
}
