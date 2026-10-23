using HexMaster.ThePrey.PlayFields.Abstractions.DataTransferObjects;
using HexMaster.ThePrey.PlayFields.DomainModels;
using HexMaster.ThePrey.PlayFields.Features.UpsertPlayField;
using HexMaster.ThePrey.PlayFields.Tests.Factories;
using Microsoft.Extensions.Logging;
using Moq;

namespace HexMaster.ThePrey.PlayFields.Tests.UpsertPlayField;

public sealed class UpsertPlayFieldCommandHandlerTests
{
    private static readonly Guid OwnerGuid = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid AttackerGuid = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private readonly Mock<IPlayFieldRepository> _mockRepository;
    private readonly Mock<ILogger<UpsertPlayFieldCommandHandler>> _mockLogger;
    private readonly UpsertPlayFieldCommandHandler _handler;

    public UpsertPlayFieldCommandHandlerTests()
    {
        _mockRepository = new Mock<IPlayFieldRepository>();
        _mockLogger = new Mock<ILogger<UpsertPlayFieldCommandHandler>>();
        _handler = new UpsertPlayFieldCommandHandler(_mockRepository.Object, _mockLogger.Object);
    }

    private static IReadOnlyList<GpsCoordinateDto> ValidSquare() =>
    [
        new GpsCoordinateDto(52.0, 5.0),
        new GpsCoordinateDto(52.0, 5.01),
        new GpsCoordinateDto(52.01, 5.01),
        new GpsCoordinateDto(52.01, 5.0)
    ];

    private static UpsertPlayFieldCommand BuildCommand(
        Guid? id = null,
        Guid? ownerId = null,
        DateTimeOffset? lastUpdatedOn = null) =>
        new(
            id ?? Guid.NewGuid(),
            ownerId ?? OwnerGuid,
            "Vondelpark",
            true,
            ValidSquare(),
            lastUpdatedOn ?? DateTimeOffset.UtcNow);

    [Fact]
    public async Task Handle_ShouldCreatePlayField_WhenNotFound()
    {
        _mockRepository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PlayField?)null);

        var command = BuildCommand();
        var result = await _handler.Handle(command, CancellationToken.None);

        var created = Assert.IsType<UpsertPlayFieldResult.Created>(result);
        Assert.Equal(command.Id, created.PlayField.Id);
        Assert.Equal("Vondelpark", created.PlayField.Name);

        _mockRepository.Verify(r =>
            r.UpsertAsync(It.Is<PlayField>(p => p.Id == command.Id), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldUpdatePlayField_WhenIncomingTimestampIsNewer()
    {
        var existingTs = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var newerTs = existingTs.AddHours(1);
        var existing = PlayFieldFaker.CreateValid(ownerId: OwnerGuid);
        var id = existing.Id;
        var rehydrated = PlayField.Rehydrate(id, existing.Name, OwnerGuid, false,
            PlayFieldFaker.SquarePoints(), existingTs);

        _mockRepository.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(rehydrated);

        var command = BuildCommand(id: id, ownerId: OwnerGuid, lastUpdatedOn: newerTs);
        var result = await _handler.Handle(command, CancellationToken.None);

        var after = DateTimeOffset.UtcNow;
        var updated = Assert.IsType<UpsertPlayFieldResult.Updated>(result);
        Assert.Equal(id, updated.PlayField.Id);
        // Handler stamps LastModifiedOn with UtcNow at the time of the update, not the command timestamp.
        Assert.InRange(updated.PlayField.LastUpdatedOn, existingTs, after);

        _mockRepository.Verify(r =>
            r.UpsertAsync(It.Is<PlayField>(p => p.Id == id), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldReturnConflict_WhenIncomingTimestampIsOlderOrEqual()
    {
        var storedTs = new DateTimeOffset(2024, 6, 1, 12, 0, 0, TimeSpan.Zero);
        var staleTs = storedTs.AddMinutes(-1);
        var id = Guid.NewGuid();
        var existing = PlayField.Rehydrate(id, "Existing", OwnerGuid, false,
            PlayFieldFaker.SquarePoints(), storedTs);

        _mockRepository.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var command = BuildCommand(id: id, ownerId: OwnerGuid, lastUpdatedOn: staleTs);
        var result = await _handler.Handle(command, CancellationToken.None);

        var conflict = Assert.IsType<UpsertPlayFieldResult.Conflict>(result);
        Assert.Equal(id, conflict.CurrentPlayField.Id);

        _mockRepository.Verify(r =>
            r.UpsertAsync(It.IsAny<PlayField>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldReturnForbidden_WhenOwnerMismatch()
    {
        var id = Guid.NewGuid();
        var existing = PlayField.Rehydrate(id, "Field", OwnerGuid, false,
            PlayFieldFaker.SquarePoints(), DateTimeOffset.UtcNow);

        _mockRepository.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var command = BuildCommand(id: id, ownerId: AttackerGuid);
        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.IsType<UpsertPlayFieldResult.Forbidden>(result);

        _mockRepository.Verify(r =>
            r.UpsertAsync(It.IsAny<PlayField>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenCommandIsNull()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _handler.Handle(null!, CancellationToken.None));
    }

    // ─── IsPublic coercion via Create branch ─────────────────────────────────

    [Fact]
    public async Task Handle_ShouldCoerceIsPublicToFalse_WhenCreatingWithNonEligibleNameAndIsPublicTrue()
    {
        _mockRepository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PlayField?)null);

        // "Vondelpark" does not match the CC, City, Fieldname convention
        var command = new UpsertPlayFieldCommand(
            Guid.NewGuid(), OwnerGuid, "Vondelpark", true, ValidSquare(), DateTimeOffset.UtcNow);

        var result = await _handler.Handle(command, CancellationToken.None);

        var created = Assert.IsType<UpsertPlayFieldResult.Created>(result);
        Assert.False(created.PlayField.IsPublic);
    }

    [Fact]
    public async Task Handle_ShouldKeepIsPublicTrue_WhenCreatingWithEligibleNameAndIsPublicTrue()
    {
        _mockRepository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PlayField?)null);

        var command = new UpsertPlayFieldCommand(
            Guid.NewGuid(), OwnerGuid, "NL, Amsterdam, Vondelpark", true, ValidSquare(), DateTimeOffset.UtcNow);

        var result = await _handler.Handle(command, CancellationToken.None);

        var created = Assert.IsType<UpsertPlayFieldResult.Created>(result);
        Assert.True(created.PlayField.IsPublic);
    }

    // ─── IsPublic coercion via Update branch ─────────────────────────────────

    [Fact]
    public async Task Handle_ShouldCoerceIsPublicToFalse_WhenUpdatingWithNonEligibleNameAndIsPublicTrue()
    {
        var existingTs = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var newerTs = existingTs.AddHours(1);
        var id = Guid.NewGuid();
        var existing = PlayField.Rehydrate(id, "NL, Amsterdam, Vondelpark", OwnerGuid, false,
            PlayFieldFaker.SquarePoints(), existingTs);

        _mockRepository.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        // Non-eligible name with isPublic=true — should be coerced to false
        var command = new UpsertPlayFieldCommand(
            id, OwnerGuid, "Vondelpark", true, ValidSquare(), newerTs);

        var result = await _handler.Handle(command, CancellationToken.None);

        var updated = Assert.IsType<UpsertPlayFieldResult.Updated>(result);
        Assert.False(updated.PlayField.IsPublic);
    }

    [Fact]
    public async Task Handle_ShouldKeepIsPublicTrue_WhenUpdatingWithEligibleNameAndIsPublicTrue()
    {
        var existingTs = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var newerTs = existingTs.AddHours(1);
        var id = Guid.NewGuid();
        var existing = PlayField.Rehydrate(id, "NL, Amsterdam, Vondelpark", OwnerGuid, false,
            PlayFieldFaker.SquarePoints(), existingTs);

        _mockRepository.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var command = new UpsertPlayFieldCommand(
            id, OwnerGuid, "NL, Amsterdam, Vondelpark", true, ValidSquare(), newerTs);

        var result = await _handler.Handle(command, CancellationToken.None);

        var updated = Assert.IsType<UpsertPlayFieldResult.Updated>(result);
        Assert.True(updated.PlayField.IsPublic);
    }
}
