using HexMaster.ThePrey.Users.DomainModels;
using HexMaster.ThePrey.Users.Features.UpdateUser;
using HexMaster.ThePrey.Users.Tests.Factories;
using Microsoft.Extensions.Logging;
using Moq;

namespace HexMaster.ThePrey.Users.Tests.UpdateUser;

public sealed class UpdateUserCommandHandlerTests
{
    private readonly Mock<IUserRepository> _mockRepository;
    private readonly UpdateUserCommandHandler _handler;

    public UpdateUserCommandHandlerTests()
    {
        _mockRepository = new Mock<IUserRepository>();
        _handler = new UpdateUserCommandHandler(_mockRepository.Object, Mock.Of<ILogger<UpdateUserCommandHandler>>());
    }

    [Fact]
    public async Task Handle_ShouldUpdateUser_WhenUserExists()
    {
        var user = UserFaker.CreateValid(subjectId: "auth0|abc");

        _mockRepository
            .Setup(r => r.GetBySubjectIdAsync("auth0|abc", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var command = new UpdateUserCommand("auth0|abc", null, null, "Ghost Rider", "nl");
        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.Equal("Ghost Rider", result.DisplayName);
        Assert.Equal("nl", result.Language);
        _mockRepository.Verify(r => r.UpdateAsync(user, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenUserDoesNotExist()
    {
        _mockRepository
            .Setup(r => r.GetBySubjectIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var command = new UpdateUserCommand("auth0|unknown", null, null, "Ghost", "en");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenCommandIsNull()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _handler.Handle(null!, CancellationToken.None));
    }
}
