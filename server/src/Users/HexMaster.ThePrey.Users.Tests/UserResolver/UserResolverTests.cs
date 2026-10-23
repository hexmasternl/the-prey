using Dapr.Client;
using HexMaster.ThePrey.Users.Abstractions.DataTransferObjects;
using HexMaster.ThePrey.Users.Integration;
using HexMaster.ThePrey.Users.Tests.Factories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.Net;

namespace HexMaster.ThePrey.Users.Tests.UserResolver;

public sealed class UserResolverTests
{
    private readonly Mock<DaprClient> _daprMock = new();
    private readonly UserResolverOptions _options = new()
    {
        StateStoreName = "statestore",
        CacheTtlSeconds = 300,
        UsersAppId = "hexmaster-theprey-users-api"
    };

    private Integration.UserResolver CreateSut() =>
        new(_daprMock.Object, Options.Create(_options), Mock.Of<ILogger<Integration.UserResolver>>());

    private UserDto CreateUserDto()
    {
        var user = UserFaker.CreateValid();
        return new UserDto(user.Id, user.DisplayName, user.Callsign, user.EmailAddress, user.PreferredLanguage);
    }

    [Fact]
    public async Task ResolveUser_ShouldReturnCachedDto_WhenStateStoreHit()
    {
        // Arrange
        const string subjectId = "auth0|cached-user";
        var cachedDto = CreateUserDto();

        _daprMock
            .Setup(d => d.GetStateAsync<UserDto>(
                _options.StateStoreName,
                $"user-subject:{subjectId}",
                It.IsAny<ConsistencyMode?>(),
                It.IsAny<IReadOnlyDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedDto);

        var sut = CreateSut();

        // Act
        var result = await sut.ResolveUser(subjectId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(cachedDto.UserId, result.UserId);
        Assert.Equal(cachedDto.DisplayName, result.DisplayName);

        // InvokeMethodAsync(HttpRequestMessage, ct) should not be called on a cache hit
#pragma warning disable CS0618 // obsolete but only Moq-able invocation overload — see UserResolver.cs
        _daprMock.Verify(
            d => d.InvokeMethodAsync<UserDto>(
                It.IsAny<HttpRequestMessage>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
#pragma warning restore CS0618
    }

    [Fact]
    public async Task ResolveUser_ShouldInvokeUsersService_WhenStateStoreMiss()
    {
        // Arrange
        const string subjectId = "auth0|fresh-user";
        var serviceDto = CreateUserDto();

        _daprMock
            .Setup(d => d.GetStateAsync<UserDto?>(
                _options.StateStoreName,
                $"user-subject:{subjectId}",
                It.IsAny<ConsistencyMode?>(),
                It.IsAny<IReadOnlyDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserDto?)null);

        // CreateInvokeMethodRequest + the HttpRequestMessage overload is the only Moq-able
        // service-invocation API; Dapr 1.17 marks it [Obsolete] (CS0618) — suppress, see UserResolver.cs.
#pragma warning disable CS0618
        _daprMock
            .Setup(d => d.InvokeMethodAsync<UserDto>(
                It.IsAny<HttpRequestMessage>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(serviceDto);
#pragma warning restore CS0618

        _daprMock
            .Setup(d => d.SaveStateAsync(
                _options.StateStoreName,
                $"user-subject:{subjectId}",
                serviceDto,
                It.IsAny<StateOptions?>(),
                It.IsAny<IReadOnlyDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var sut = CreateSut();

        // Act
        var result = await sut.ResolveUser(subjectId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(serviceDto.UserId, result.UserId);

        _daprMock.Verify(
            d => d.SaveStateAsync(
                _options.StateStoreName,
                $"user-subject:{subjectId}",
                serviceDto,
                It.IsAny<StateOptions?>(),
                It.IsAny<IReadOnlyDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ResolveUser_ShouldReturnNull_WhenUsersServiceReturns404()
    {
        // Arrange
        const string subjectId = "auth0|not-found";

        _daprMock
            .Setup(d => d.GetStateAsync<UserDto?>(
                _options.StateStoreName,
                $"user-subject:{subjectId}",
                It.IsAny<ConsistencyMode?>(),
                It.IsAny<IReadOnlyDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserDto?)null);

        var notFoundResponse = new HttpResponseMessage(HttpStatusCode.NotFound);
        var invocationException = new InvocationException(
            _options.UsersAppId,
            $"internal/users/{subjectId}",
            new Exception("Not Found"),
            notFoundResponse);

#pragma warning disable CS0618 // obsolete but only Moq-able invocation overload — see UserResolver.cs
        _daprMock
            .Setup(d => d.InvokeMethodAsync<UserDto>(
                It.IsAny<HttpRequestMessage>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(invocationException);
#pragma warning restore CS0618

        var sut = CreateSut();

        // Act
        var result = await sut.ResolveUser(subjectId);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task ResolveUser_ShouldPropagate_WhenGetStateThrows()
    {
        // Arrange
        const string subjectId = "auth0|error-user";

        _daprMock
            .Setup(d => d.GetStateAsync<UserDto>(
                _options.StateStoreName,
                $"user-subject:{subjectId}",
                It.IsAny<ConsistencyMode?>(),
                It.IsAny<IReadOnlyDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("state store unavailable"));

        var sut = CreateSut();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.ResolveUser(subjectId));
    }
}
