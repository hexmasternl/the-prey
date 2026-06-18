using HexMaster.ThePrey.PlayFields.Abstractions.DataTransferObjects;
using HexMaster.ThePrey.PlayFields.DomainModels;
using HexMaster.ThePrey.PlayFields.Features.CreatePlayField;
using HexMaster.ThePrey.PlayFields.Observability;
using Microsoft.Extensions.Logging;
using Moq;

namespace HexMaster.ThePrey.PlayFields.Tests.CreatePlayField;

public sealed class CreatePlayFieldCommandHandlerTests
{
    private readonly Mock<IPlayFieldRepository> _mockRepository;
    private readonly Mock<IPlayFieldMetrics> _mockMetrics;
    private readonly Mock<ILogger<CreatePlayFieldCommandHandler>> _mockLogger;
    private readonly CreatePlayFieldCommandHandler _handler;

    private static readonly Guid OwnerId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    public CreatePlayFieldCommandHandlerTests()
    {
        _mockRepository = new Mock<IPlayFieldRepository>();
        _mockMetrics = new Mock<IPlayFieldMetrics>();
        _mockLogger = new Mock<ILogger<CreatePlayFieldCommandHandler>>();
        _handler = new CreatePlayFieldCommandHandler(_mockRepository.Object, _mockMetrics.Object, _mockLogger.Object);
    }

    private static IReadOnlyList<GpsCoordinateDto> ValidSquare() =>
    [
        new GpsCoordinateDto(52.0, 5.0),
        new GpsCoordinateDto(52.0, 5.01),
        new GpsCoordinateDto(52.01, 5.01),
        new GpsCoordinateDto(52.01, 5.0)
    ];

    [Fact]
    public async Task Handle_ShouldCreateAndPersist_WhenCommandIsValid()
    {
        var command = new CreatePlayFieldCommand(OwnerId, "NL, Amsterdam, Vondelpark", true, ValidSquare());

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.NotEqual(Guid.Empty, result.PlayField.Id);
        Assert.Equal("NL, Amsterdam, Vondelpark", result.PlayField.Name);
        Assert.Equal(OwnerId, result.PlayField.OwnerId);
        Assert.True(result.PlayField.IsPublic);
        Assert.Equal(4, result.PlayField.Points.Count);
        _mockRepository.Verify(r => r.AddAsync(It.IsAny<PlayField>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockMetrics.Verify(m => m.RecordPlayFieldCreated(), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenCommandIsNull()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _handler.Handle(null!, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenTooFewPoints()
    {
        var command = new CreatePlayFieldCommand(
            OwnerId,
            "Field",
            false,
            [new GpsCoordinateDto(52.0, 5.0), new GpsCoordinateDto(52.0, 5.01)]);

        await Assert.ThrowsAsync<ArgumentException>(() => _handler.Handle(command, CancellationToken.None));
        _mockRepository.Verify(r => r.AddAsync(It.IsAny<PlayField>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenCoordinateOutOfRange()
    {
        var command = new CreatePlayFieldCommand(
            OwnerId,
            "Field",
            false,
            [new GpsCoordinateDto(200.0, 5.0), new GpsCoordinateDto(52.0, 5.01), new GpsCoordinateDto(52.01, 5.01)]);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => _handler.Handle(command, CancellationToken.None));
        _mockRepository.Verify(r => r.AddAsync(It.IsAny<PlayField>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldCoerceIsPublicToFalse_WhenNameNotEligibleAndIsPublicRequested()
    {
        var command = new CreatePlayFieldCommand(OwnerId, "Vondelpark", true, ValidSquare());

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.False(result.PlayField.IsPublic);
        _mockRepository.Verify(r => r.AddAsync(It.IsAny<PlayField>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldKeepIsPublicTrue_WhenNameIsEligibleAndIsPublicRequested()
    {
        var command = new CreatePlayFieldCommand(OwnerId, "NL, Amsterdam, Vondelpark", true, ValidSquare());

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.PlayField.IsPublic);
        _mockRepository.Verify(r => r.AddAsync(It.IsAny<PlayField>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
