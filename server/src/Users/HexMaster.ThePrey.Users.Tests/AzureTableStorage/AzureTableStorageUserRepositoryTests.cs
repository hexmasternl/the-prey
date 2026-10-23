using Azure;
using Azure.Data.Tables;
using Azure.Data.Tables.Models;
using HexMaster.ThePrey.Users.Data.AzureTableStorage;
using HexMaster.ThePrey.Users.DomainModels;
using HexMaster.ThePrey.Users.Tests.Factories;
using Moq;

namespace HexMaster.ThePrey.Users.Tests.AzureTableStorage;

public sealed class AzureTableStorageUserRepositoryTests
{
    private readonly Mock<TableServiceClient> _serviceClientMock = new();
    private readonly Mock<TableClient> _tableClientMock = new();
    private readonly AzureTableStorageUserRepository _sut;

    public AzureTableStorageUserRepositoryTests()
    {
        _serviceClientMock
            .Setup(s => s.GetTableClient(AzureTableStorageUserRepository.TableName))
            .Returns(_tableClientMock.Object);

        _tableClientMock
            .Setup(t => t.CreateIfNotExistsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((Response<TableItem>?)null);

        _sut = new AzureTableStorageUserRepository(_serviceClientMock.Object);
    }

    [Fact]
    public async Task GetBySubjectIdAsync_ShouldReturnUser_WhenUserExists()
    {
        // Arrange
        var user = UserFaker.CreateValid(subjectId: "auth0|test");

        var entity = new UserTableEntity
        {
            PartitionKey = user.SubjectId,
            RowKey = UserTableEntity.RowKeyValue,
            Id = user.Id.ToString(),
            FirstName = user.FirstName,
            LastName = user.LastName,
            DisplayName = user.DisplayName,
            Callsign = user.Callsign,
            EmailAddress = user.EmailAddress,
            IsEmailVerified = user.IsEmailVerified,
            PreferredLanguage = user.PreferredLanguage
        };

        var responseMock = new Mock<Response<UserTableEntity>>();
        responseMock.SetupGet(r => r.Value).Returns(entity);

        _tableClientMock
            .Setup(t => t.GetEntityAsync<UserTableEntity>(
                user.SubjectId,
                UserTableEntity.RowKeyValue,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(responseMock.Object);

        // Act
        var result = await _sut.GetBySubjectIdAsync(user.SubjectId, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(user.Id, result!.Id);
        Assert.Equal(user.SubjectId, result.SubjectId);
        Assert.Equal(user.EmailAddress, result.EmailAddress);
    }

    [Fact]
    public async Task GetBySubjectIdAsync_ShouldReturnNull_WhenNotFound()
    {
        // Arrange
        _tableClientMock
            .Setup(t => t.GetEntityAsync<UserTableEntity>(
                It.IsAny<string>(),
                It.IsAny<string>(),
                null,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RequestFailedException(404, "Not Found"));

        // Act
        var result = await _sut.GetBySubjectIdAsync("auth0|notexist", CancellationToken.None);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task AddAsync_ShouldCallAddEntity_WhenUserIsValid()
    {
        // Arrange
        var user = UserFaker.CreateValid();

        _tableClientMock
            .Setup(t => t.AddEntityAsync(
                It.IsAny<UserTableEntity>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<Response>());

        // Act
        await _sut.AddAsync(user, CancellationToken.None);

        // Assert
        _tableClientMock.Verify(
            t => t.AddEntityAsync(
                It.Is<UserTableEntity>(e =>
                    e.PartitionKey == user.SubjectId &&
                    e.RowKey == UserTableEntity.RowKeyValue &&
                    e.Id == user.Id.ToString()),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_ShouldCallUpsertEntity_WhenUserIsValid()
    {
        // Arrange
        var user = UserFaker.CreateValid();

        _tableClientMock
            .Setup(t => t.UpsertEntityAsync(
                It.IsAny<UserTableEntity>(),
                TableUpdateMode.Replace,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<Response>());

        // Act
        await _sut.UpdateAsync(user, CancellationToken.None);

        // Assert
        _tableClientMock.Verify(
            t => t.UpsertEntityAsync(
                It.Is<UserTableEntity>(e =>
                    e.PartitionKey == user.SubjectId &&
                    e.RowKey == UserTableEntity.RowKeyValue),
                TableUpdateMode.Replace,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AddAsync_ShouldThrow_WhenUserIsNull()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _sut.AddAsync(null!, CancellationToken.None));
    }

    [Fact]
    public async Task UpdateAsync_ShouldThrow_WhenUserIsNull()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _sut.UpdateAsync(null!, CancellationToken.None));
    }

    [Fact]
    public async Task GetBySubjectIdAsync_ShouldThrow_WhenSubjectIdIsEmpty()
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => _sut.GetBySubjectIdAsync(string.Empty, CancellationToken.None));
    }
}
