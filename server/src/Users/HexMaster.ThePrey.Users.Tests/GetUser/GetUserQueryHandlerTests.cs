using HexMaster.ThePrey.Users.Abstractions.DataTransferObjects;
using HexMaster.ThePrey.Users.DomainModels;
using HexMaster.ThePrey.Users.Features.GetUser;
using HexMaster.ThePrey.Users.Services;
using HexMaster.ThePrey.Users.Tests.Factories;
using Microsoft.Extensions.Logging;
using Moq;

namespace HexMaster.ThePrey.Users.Tests.GetUser;

public sealed class GetUserQueryHandlerTests
{
    private readonly Mock<IUserRepository> _mockRepository = new();
    private readonly Mock<IUserCacheService> _mockCache = new();
    private readonly GetUserQueryHandler _handler;

    public GetUserQueryHandlerTests()
    {
        _handler = new GetUserQueryHandler(
            _mockRepository.Object,
            _mockCache.Object,
            Mock.Of<ILogger<GetUserQueryHandler>>());
    }

    [Fact]
    public async Task Handle_ShouldReturnUserDto_WhenUserExists()
    {
        var user = UserFaker.CreateValid(subjectId: "auth0|xyz");

        _mockRepository
            .Setup(r => r.GetBySubjectIdAsync("auth0|xyz", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var result = await _handler.Handle(new GetUserQuery("auth0|xyz"), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(user.Id, result!.UserId);
        Assert.Equal(user.DisplayName, result.DisplayName);
        Assert.Equal(user.EmailAddress, result.EmailAddress);
    }

    [Fact]
    public async Task Handle_ShouldReturnNull_WhenUserDoesNotExist()
    {
        _mockRepository
            .Setup(r => r.GetBySubjectIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var result = await _handler.Handle(new GetUserQuery("auth0|unknown"), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenQueryIsNull()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _handler.Handle(null!, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ShouldPopulateCache_WhenUserFoundInRepository()
    {
        // Arrange
        const string subjectId = "auth0|cache-test";
        var user = UserFaker.CreateValid(subjectId: subjectId);

        _mockRepository
            .Setup(r => r.GetBySubjectIdAsync(subjectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        await _handler.Handle(new GetUserQuery(subjectId), CancellationToken.None);

        // Assert
        _mockCache.Verify(
            c => c.SetAsync(
                subjectId,
                It.Is<UserCacheEntry>(e =>
                    e.UserId == user.Id &&
                    e.Callsign == user.Callsign &&
                    e.PreferredLanguage == user.PreferredLanguage),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldNotPopulateCache_WhenUserNotFound()
    {
        // Arrange
        _mockRepository
            .Setup(r => r.GetBySubjectIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        // Act
        await _handler.Handle(new GetUserQuery("auth0|ghost"), CancellationToken.None);

        // Assert
        _mockCache.Verify(
            c => c.SetAsync(It.IsAny<string>(), It.IsAny<UserCacheEntry>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
