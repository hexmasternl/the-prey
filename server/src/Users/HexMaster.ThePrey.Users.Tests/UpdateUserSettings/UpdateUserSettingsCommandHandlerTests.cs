using HexMaster.ThePrey.Users.DomainModels;
using HexMaster.ThePrey.Users.Features.UpdateUserSettings;
using HexMaster.ThePrey.Users.Services;
using HexMaster.ThePrey.Users.Tests.Factories;
using Microsoft.Extensions.Logging;
using Moq;

namespace HexMaster.ThePrey.Users.Tests.UpdateUserSettings;

public sealed class UpdateUserSettingsCommandHandlerTests
{
    private readonly Mock<IUserRepository> _mockRepository = new();
    private readonly Mock<IUserCacheService> _mockCache = new();
    private readonly UpdateUserSettingsCommandHandler _handler;

    public UpdateUserSettingsCommandHandlerTests()
    {
        _handler = new UpdateUserSettingsCommandHandler(
            _mockRepository.Object,
            _mockCache.Object,
            Mock.Of<ILogger<UpdateUserSettingsCommandHandler>>());
    }

    [Fact]
    public async Task Handle_ShouldUpdateSettings_WhenUserExists()
    {
        var user = UserFaker.CreateValid(subjectId: "auth0|abc");

        _mockRepository
            .Setup(r => r.GetBySubjectIdAsync("auth0|abc", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var command = new UpdateUserSettingsCommand("auth0|abc", "Night-Hawk_7", "nl");
        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.Equal("Night-Hawk_7", result.Callsign);
        Assert.Equal("nl", result.PreferredLanguage);
        _mockRepository.Verify(r => r.UpdateAsync(user, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenUserDoesNotExist()
    {
        _mockRepository
            .Setup(r => r.GetBySubjectIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var command = new UpdateUserSettingsCommand("auth0|unknown", "Reaper", "en");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _handler.Handle(command, CancellationToken.None));
        _mockRepository.Verify(r => r.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenCallsignIsInvalid()
    {
        var user = UserFaker.CreateValid(subjectId: "auth0|abc");

        _mockRepository
            .Setup(r => r.GetBySubjectIdAsync("auth0|abc", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var command = new UpdateUserSettingsCommand("auth0|abc", "x!", "en");

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _handler.Handle(command, CancellationToken.None));
        _mockRepository.Verify(r => r.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenPreferredLanguageIsNotSupported()
    {
        var user = UserFaker.CreateValid(subjectId: "auth0|abc");

        _mockRepository
            .Setup(r => r.GetBySubjectIdAsync("auth0|abc", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var command = new UpdateUserSettingsCommand("auth0|abc", "Reaper", "de");

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _handler.Handle(command, CancellationToken.None));
        _mockRepository.Verify(r => r.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenCommandIsNull()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _handler.Handle(null!, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ShouldUpdateCache_AfterSettingsUpdated()
    {
        // Arrange
        const string subjectId = "auth0|cache";
        var user = UserFaker.CreateValid(subjectId: subjectId);

        _mockRepository
            .Setup(r => r.GetBySubjectIdAsync(subjectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var command = new UpdateUserSettingsCommand(subjectId, "NightOwl", "nl");

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert — cache should be updated with the new callsign
        _mockCache.Verify(
            c => c.SetAsync(
                subjectId,
                It.Is<UserCacheEntry>(e =>
                    e.UserId == user.Id &&
                    e.Callsign == "NightOwl" &&
                    e.PreferredLanguage == "nl"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldNotUpdateCache_WhenUserDoesNotExist()
    {
        // Arrange
        _mockRepository
            .Setup(r => r.GetBySubjectIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var command = new UpdateUserSettingsCommand("auth0|ghost", "Reaper", "en");

        // Act
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _handler.Handle(command, CancellationToken.None));

        // Assert
        _mockCache.Verify(
            c => c.SetAsync(It.IsAny<string>(), It.IsAny<UserCacheEntry>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
