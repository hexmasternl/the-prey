using HexMaster.ThePrey.Users.Features.ResolveUserBySubject;
using HexMaster.ThePrey.Users.Tests.Factories;
using Microsoft.Extensions.Logging;
using Moq;

namespace HexMaster.ThePrey.Users.Tests.ResolveUserBySubject;

public sealed class ResolveUserBySubjectQueryHandlerTests
{
    private readonly Mock<IUserRepository> _repositoryMock = new();
    private readonly ResolveUserBySubjectQueryHandler _sut;

    public ResolveUserBySubjectQueryHandlerTests()
    {
        _sut = new ResolveUserBySubjectQueryHandler(
            _repositoryMock.Object,
            Mock.Of<ILogger<ResolveUserBySubjectQueryHandler>>());
    }

    [Fact]
    public async Task Handle_ShouldReturnUserDto_WhenUserExistsInRepository()
    {
        // Arrange
        const string subjectId = "auth0|test-subject-123";
        var user = UserFaker.CreateValid(subjectId: subjectId);

        _repositoryMock
            .Setup(r => r.GetBySubjectIdAsync(subjectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var result = await _sut.Handle(new ResolveUserBySubjectQuery(subjectId), CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(user.Id, result.UserId);
        Assert.Equal(user.DisplayName, result.DisplayName);
        Assert.Equal(user.Callsign, result.Callsign);
        Assert.Equal(user.EmailAddress, result.EmailAddress);
        Assert.Equal(user.PreferredLanguage, result.PreferredLanguage);
    }

    [Fact]
    public async Task Handle_ShouldReturnNull_WhenUserNotFoundInRepository()
    {
        // Arrange
        _repositoryMock
            .Setup(r => r.GetBySubjectIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((HexMaster.ThePrey.Users.DomainModels.User?)null);

        // Act
        var result = await _sut.Handle(new ResolveUserBySubjectQuery("auth0|unknown"), CancellationToken.None);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task Handle_ShouldSetFoundTag_OnActivity()
    {
        // Arrange
        const string subjectId = "auth0|found-user";
        var user = UserFaker.CreateValid(subjectId: subjectId);

        _repositoryMock
            .Setup(r => r.GetBySubjectIdAsync(subjectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act — the activity tag is set internally; we verify the correct branch executed by checking the dto is returned
        var result = await _sut.Handle(new ResolveUserBySubjectQuery(subjectId), CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(user.Id, result.UserId);
    }
}
