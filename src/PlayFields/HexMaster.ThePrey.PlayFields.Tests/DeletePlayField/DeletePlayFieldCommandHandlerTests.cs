using HexMaster.ThePrey.PlayFields.DomainModels;
using HexMaster.ThePrey.PlayFields.Features.DeletePlayField;
using HexMaster.ThePrey.PlayFields.Observability;
using HexMaster.ThePrey.PlayFields.Tests.Factories;
using Microsoft.Extensions.Logging;
using Moq;

namespace HexMaster.ThePrey.PlayFields.Tests.DeletePlayField;

public sealed class DeletePlayFieldCommandHandlerTests
{
    private readonly Mock<IPlayFieldRepository> _repositoryMock = new();
    private readonly Mock<IPlayFieldMetrics> _metricsMock = new();
    private readonly Mock<ILogger<DeletePlayFieldCommandHandler>> _loggerMock = new();
    private readonly DeletePlayFieldCommandHandler _sut;

    public DeletePlayFieldCommandHandlerTests()
    {
        _sut = new DeletePlayFieldCommandHandler(
            _repositoryMock.Object,
            _metricsMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_ShouldReturnSuccess_WhenPlayFieldExistsAndCallerIsOwner()
    {
        // Arrange
        var playField = PlayFieldFaker.CreateValid();
        var command = new DeletePlayFieldCommand(playField.Id, playField.OwnerId);

        _repositoryMock
            .Setup(r => r.GetByIdAsync(playField.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(playField);

        _repositoryMock
            .Setup(r => r.DeleteAsync(playField.Id, playField.OwnerId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        Assert.IsType<DeletePlayFieldResult.Success>(result);
        _repositoryMock.Verify(r => r.DeleteAsync(playField.Id, playField.OwnerId, It.IsAny<CancellationToken>()), Times.Once);
        _metricsMock.Verify(m => m.RecordPlayFieldDeleted(), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldReturnNotFound_WhenPlayFieldDoesNotExist()
    {
        // Arrange
        var id = Guid.NewGuid();
        var command = new DeletePlayFieldCommand(id, "auth0|someowner");

        _repositoryMock
            .Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PlayField?)null);

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        Assert.IsType<DeletePlayFieldResult.NotFound>(result);
        _repositoryMock.Verify(r => r.DeleteAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _metricsMock.Verify(m => m.RecordPlayFieldDeleted(), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldReturnForbidden_WhenCallerIsNotTheOwner()
    {
        // Arrange
        var playField = PlayFieldFaker.CreateValid(ownerId: "auth0|actualowner");
        var command = new DeletePlayFieldCommand(playField.Id, "auth0|differentuser");

        _repositoryMock
            .Setup(r => r.GetByIdAsync(playField.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(playField);

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        Assert.IsType<DeletePlayFieldResult.Forbidden>(result);
        _repositoryMock.Verify(r => r.DeleteAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _metricsMock.Verify(m => m.RecordPlayFieldDeleted(), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenCommandIsNull()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _sut.Handle(null!, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ShouldPropagateException_WhenRepositoryGetThrows()
    {
        // Arrange
        var command = new DeletePlayFieldCommand(Guid.NewGuid(), "auth0|owner");

        _repositoryMock
            .Setup(r => r.GetByIdAsync(command.PlayFieldId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Storage unavailable"));

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.Handle(command, CancellationToken.None));
        _repositoryMock.Verify(r => r.DeleteAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _metricsMock.Verify(m => m.RecordPlayFieldDeleted(), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldPropagateException_WhenRepositoryDeleteThrows()
    {
        // Arrange
        var playField = PlayFieldFaker.CreateValid();
        var command = new DeletePlayFieldCommand(playField.Id, playField.OwnerId);

        _repositoryMock
            .Setup(r => r.GetByIdAsync(playField.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(playField);

        _repositoryMock
            .Setup(r => r.DeleteAsync(playField.Id, playField.OwnerId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Storage unavailable"));

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.Handle(command, CancellationToken.None));
        _metricsMock.Verify(m => m.RecordPlayFieldDeleted(), Times.Never);
    }
}
