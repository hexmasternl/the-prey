using HexMaster.ThePrey.Users.Abstractions.DataTransferObjects;
using HexMaster.ThePrey.Users.DomainModels;
using HexMaster.ThePrey.Users.Features.CreateUser;
using HexMaster.ThePrey.Users.Observability;
using HexMaster.ThePrey.Users.Services;
using HexMaster.ThePrey.Users.Tests.Factories;
using Microsoft.Extensions.Logging;
using Moq;

namespace HexMaster.ThePrey.Users.Tests.CreateUser;

public sealed class CreateUserCommandHandlerTests
{
    private readonly Mock<IUserRepository> _mockRepository = new();
    private readonly Mock<IUserCacheService> _mockCache = new();
    private readonly Mock<IUserMetrics> _mockMetrics = new();
    private readonly Mock<ILogger<CreateUserCommandHandler>> _mockLogger = new();
    private readonly CreateUserCommandHandler _handler;

    public CreateUserCommandHandlerTests()
    {
        _handler = new CreateUserCommandHandler(
            _mockRepository.Object,
            _mockCache.Object,
            _mockMetrics.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_ShouldCreateUser_WhenUserDoesNotExist()
    {
        var command = new CreateUserCommand("auth0|abc", "Alice", "Smith", "alice@example.com", true, "en");

        _mockRepository
            .Setup(r => r.GetBySubjectIdAsync(command.SubjectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.WasCreated);
        Assert.Equal("Alice", result.User.DisplayName);
        Assert.Equal("alice@example.com", result.User.EmailAddress);
        _mockRepository.Verify(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldReturnWasCreatedFalse_WhenUserAlreadyExists()
    {
        var existing = UserFaker.CreateValid(subjectId: "auth0|abc", email: "alice@example.com");
        var command = new CreateUserCommand("auth0|abc", "Alice", "Smith", "alice@example.com", true, "en");

        _mockRepository
            .Setup(r => r.GetBySubjectIdAsync(command.SubjectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.False(result.WasCreated);
        _mockRepository.Verify(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockRepository.Verify(r => r.UpdateAsync(existing, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenCommandIsNull()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _handler.Handle(null!, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ShouldSetDisplayNameToEmail_WhenFirstNameIsNull()
    {
        var command = new CreateUserCommand("auth0|abc", null, null, "noname@example.com", true, "en");

        _mockRepository
            .Setup(r => r.GetBySubjectIdAsync(command.SubjectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.Equal("noname@example.com", result.User.DisplayName);
    }

    [Fact]
    public async Task Handle_ShouldPopulateCache_AfterUserCreated()
    {
        // Arrange
        var command = new CreateUserCommand("auth0|new", "Bob", "Jones", "bob@example.com", true, "en");

        _mockRepository
            .Setup(r => r.GetBySubjectIdAsync(command.SubjectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _mockCache.Verify(
            c => c.SetAsync(
                command.SubjectId,
                It.Is<UserCacheEntry>(e => e.PreferredLanguage == "en"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldPopulateCache_AfterExistingUserSynced()
    {
        // Arrange
        var existing = UserFaker.CreateValid(subjectId: "auth0|existing");
        var command = new CreateUserCommand("auth0|existing", "Updated", null, "updated@example.com", true, "en");

        _mockRepository
            .Setup(r => r.GetBySubjectIdAsync(command.SubjectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _mockCache.Verify(
            c => c.SetAsync(
                command.SubjectId,
                It.Is<UserCacheEntry>(e => e.UserId == existing.Id),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
