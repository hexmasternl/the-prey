using Dapr.Client;
using HexMaster.ThePrey.Users.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace HexMaster.ThePrey.Users.Tests.Services;

public sealed class UserCacheServiceTests
{
    private const string StateStoreName = "statestore";

    private readonly Mock<DaprClient> _daprClientMock = new();
    private readonly UserCacheService _sut;

    public UserCacheServiceTests()
    {
        _sut = new UserCacheService(
            _daprClientMock.Object,
            Mock.Of<ILogger<UserCacheService>>());
    }

    [Fact]
    public async Task GetAsync_ShouldReturnEntry_WhenStateStoreHasValue()
    {
        // Arrange
        const string subjectId = "auth0|cached";
        var expected = new UserCacheEntry(Guid.NewGuid(), "Reaper", "en");

        _daprClientMock
            .Setup(d => d.GetStateAsync<UserCacheEntry>(
                StateStoreName,
                $"theprey:users:by-subject:{subjectId}",
                It.IsAny<ConsistencyMode?>(),
                It.IsAny<IReadOnlyDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        // Act
        var result = await _sut.GetAsync(subjectId, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expected.UserId, result!.UserId);
        Assert.Equal(expected.Callsign, result.Callsign);
        Assert.Equal(expected.PreferredLanguage, result.PreferredLanguage);
    }

    [Fact]
    public async Task GetAsync_ShouldReturnNull_WhenStateStoreReturnsNull()
    {
        // Arrange
        const string subjectId = "auth0|missing";

        _daprClientMock
            .Setup(d => d.GetStateAsync<UserCacheEntry?>(
                StateStoreName,
                $"theprey:users:by-subject:{subjectId}",
                It.IsAny<ConsistencyMode?>(),
                It.IsAny<IReadOnlyDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserCacheEntry?)null);

        // Act
        var result = await _sut.GetAsync(subjectId, CancellationToken.None);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetAsync_ShouldReturnNull_WhenDaprClientThrows()
    {
        // Arrange
        const string subjectId = "auth0|error";

        _daprClientMock
            .Setup(d => d.GetStateAsync<UserCacheEntry>(
                StateStoreName,
                It.IsAny<string>(),
                It.IsAny<ConsistencyMode?>(),
                It.IsAny<IReadOnlyDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Dapr unavailable"));

        // Act — must not throw
        var result = await _sut.GetAsync(subjectId, CancellationToken.None);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task SetAsync_ShouldCallSaveStateAsync_WithCorrectKeyAndEntry()
    {
        // Arrange
        const string subjectId = "auth0|save";
        var entry = new UserCacheEntry(Guid.NewGuid(), "Ghost", "nl");
        var expectedKey = $"theprey:users:by-subject:{subjectId}";

        _daprClientMock
            .Setup(d => d.SaveStateAsync(
                StateStoreName,
                expectedKey,
                entry,
                It.IsAny<StateOptions?>(),
                It.IsAny<IReadOnlyDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _sut.SetAsync(subjectId, entry, CancellationToken.None);

        // Assert
        _daprClientMock.Verify(
            d => d.SaveStateAsync(
                StateStoreName,
                expectedKey,
                entry,
                It.IsAny<StateOptions?>(),
                It.IsAny<IReadOnlyDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetAsync_ShouldThrow_WhenSubjectIdIsEmpty()
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => _sut.GetAsync(string.Empty, CancellationToken.None));
    }

    [Fact]
    public async Task SetAsync_ShouldThrow_WhenSubjectIdIsEmpty()
    {
        var entry = new UserCacheEntry(Guid.NewGuid(), "Ghost", "en");
        await Assert.ThrowsAsync<ArgumentException>(
            () => _sut.SetAsync(string.Empty, entry, CancellationToken.None));
    }

    [Fact]
    public async Task SetAsync_ShouldThrow_WhenEntryIsNull()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _sut.SetAsync("auth0|x", null!, CancellationToken.None));
    }
}
