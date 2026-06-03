using HexMaster.ThePrey.PlayFields.DomainModels;
using HexMaster.ThePrey.PlayFields.Features.GetPlayField;
using HexMaster.ThePrey.PlayFields.Tests.Factories;
using Moq;

namespace HexMaster.ThePrey.PlayFields.Tests.GetPlayField;

public sealed class GetPlayFieldQueryHandlerTests
{
    private readonly Mock<IPlayFieldRepository> _mockRepository;
    private readonly GetPlayFieldQueryHandler _handler;

    public GetPlayFieldQueryHandlerTests()
    {
        _mockRepository = new Mock<IPlayFieldRepository>();
        _handler = new GetPlayFieldQueryHandler(_mockRepository.Object);
    }

    [Fact]
    public async Task Handle_ShouldReturnDto_WhenOwnerRequestsOwnPrivateField()
    {
        var playField = PlayFieldFaker.CreateValid(ownerId: "auth0|owner", isPublic: false);
        _mockRepository.Setup(r => r.GetByIdAsync(playField.Id, It.IsAny<CancellationToken>())).ReturnsAsync(playField);

        var result = await _handler.Handle(new GetPlayFieldQuery(playField.Id, "auth0|owner"), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(playField.Id, result!.Id);
    }

    [Fact]
    public async Task Handle_ShouldReturnNull_WhenOtherRequestsPrivateField()
    {
        var playField = PlayFieldFaker.CreateValid(ownerId: "auth0|owner", isPublic: false);
        _mockRepository.Setup(r => r.GetByIdAsync(playField.Id, It.IsAny<CancellationToken>())).ReturnsAsync(playField);

        var result = await _handler.Handle(new GetPlayFieldQuery(playField.Id, "auth0|other"), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task Handle_ShouldReturnDto_WhenOtherRequestsPublicField()
    {
        var playField = PlayFieldFaker.CreateValid(ownerId: "auth0|owner", isPublic: true);
        _mockRepository.Setup(r => r.GetByIdAsync(playField.Id, It.IsAny<CancellationToken>())).ReturnsAsync(playField);

        var result = await _handler.Handle(new GetPlayFieldQuery(playField.Id, "auth0|other"), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(playField.Id, result!.Id);
    }

    [Fact]
    public async Task Handle_ShouldReturnNull_WhenFieldDoesNotExist()
    {
        _mockRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PlayField?)null);

        var result = await _handler.Handle(new GetPlayFieldQuery(Guid.NewGuid(), "auth0|owner"), CancellationToken.None);

        Assert.Null(result);
    }
}
