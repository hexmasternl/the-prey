using HexMaster.ThePrey.Games.DomainModels;
using HexMaster.ThePrey.Games.Features.ExportGames;
using HexMaster.ThePrey.Games.Tests.Factories;
using Moq;

namespace HexMaster.ThePrey.Games.Tests.Features.ExportGames;

public sealed class ExportGamesQueryHandlerTests
{
    private static readonly DateTimeOffset BaseDate = new(2026, 6, 17, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset StartedAt = BaseDate.AddHours(9);

    private readonly Mock<IGameRepository> _repositoryMock = new();
    private readonly ExportGamesQueryHandler _sut;

    public ExportGamesQueryHandlerTests()
    {
        _sut = new ExportGamesQueryHandler(_repositoryMock.Object);
    }

    [Fact]
    public async Task Handle_ShouldReturnMappedDtos_WhenGamesExistInRange()
    {
        // Arrange
        var game = GameFaker.StartedGame(out _, out _, StartedAt);
        _repositoryMock
            .Setup(r => r.GetGamesStartedBetweenAsync(BaseDate, BaseDate.AddDays(1), It.IsAny<CancellationToken>()))
            .ReturnsAsync([game]);

        var query = new ExportGamesQuery(BaseDate, BaseDate.AddDays(1));

        // Act
        var result = await _sut.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        var dto = result[0];
        Assert.Equal(game.Id, dto.Id);
        Assert.Equal(game.GameCode, dto.GameCode);
        Assert.Equal(game.PlayfieldId, dto.PlayfieldId);
        Assert.Equal(game.OwnerUserId, dto.OwnerUserId);
        Assert.Equal(game.Status.ToString(), dto.Status);
        Assert.Equal(game.StartedAt, dto.StartedAt);
        Assert.Equal(game.Outcome.ToString(), dto.Outcome);
        Assert.Equal(game.HunterUserId, dto.HunterUserId);
        Assert.Equal(game.Participants.Count, dto.Participants.Count);
    }

    [Fact]
    public async Task Handle_ShouldReturnEmptyList_WhenNoGamesInRange()
    {
        // Arrange
        _repositoryMock
            .Setup(r => r.GetGamesStartedBetweenAsync(It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var query = new ExportGamesQuery(BaseDate, BaseDate.AddDays(1));

        // Act
        var result = await _sut.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task Handle_ShouldPassCorrectWindowToRepository()
    {
        // Arrange
        var from = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
        var to = from.AddDays(1);
        _repositoryMock
            .Setup(r => r.GetGamesStartedBetweenAsync(from, to, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var query = new ExportGamesQuery(from, to);

        // Act
        await _sut.Handle(query, CancellationToken.None);

        // Assert — verify the exact window was forwarded to the repository
        _repositoryMock.Verify(
            r => r.GetGamesStartedBetweenAsync(from, to, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldFlagHunterCorrectly_WhenGameHasHunter()
    {
        // Arrange
        var game = GameFaker.StartedGame(out var hunterId, out var preyIds, StartedAt);
        _repositoryMock
            .Setup(r => r.GetGamesStartedBetweenAsync(It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([game]);

        var query = new ExportGamesQuery(BaseDate, BaseDate.AddDays(1));

        // Act
        var result = await _sut.Handle(query, CancellationToken.None);

        // Assert
        var dto = result[0];
        var hunterParticipant = dto.Participants.SingleOrDefault(p => p.UserId == hunterId);
        Assert.NotNull(hunterParticipant);
        Assert.True(hunterParticipant!.IsHunter);

        foreach (var prey in dto.Participants.Where(p => p.UserId != hunterId))
            Assert.False(prey.IsHunter);
    }

    [Fact]
    public async Task Handle_ShouldIncludeAllLocations_WhenParticipantHasLocationReadings()
    {
        // Arrange
        var game = GameFaker.StartedGame(out _, out var preyIds, StartedAt);
        var prey = game.Participants.First(p => p.UserId == preyIds[0]);

        // Record two locations for the first prey
        var coord1 = GpsCoordinate.Create(52.0, 5.0);
        var coord2 = GpsCoordinate.Create(52.001, 5.001);
        game.RecordLocation(preyIds[0], coord1, StartedAt.AddMinutes(1));
        game.RecordLocation(preyIds[0], coord2, StartedAt.AddMinutes(2));

        _repositoryMock
            .Setup(r => r.GetGamesStartedBetweenAsync(It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([game]);

        var query = new ExportGamesQuery(BaseDate, BaseDate.AddDays(1));

        // Act
        var result = await _sut.Handle(query, CancellationToken.None);

        // Assert
        var dto = result[0];
        var preyDto = dto.Participants.Single(p => p.UserId == preyIds[0]);
        Assert.Equal(2, preyDto.Locations.Count);
        Assert.Contains(preyDto.Locations, l => l.Coordinate.Latitude == 52.0 && l.Coordinate.Longitude == 5.0);
        Assert.Contains(preyDto.Locations, l => l.Coordinate.Latitude == 52.001 && l.Coordinate.Longitude == 5.001);
    }

    [Fact]
    public async Task Handle_ShouldIncludeAllPenalties_WhenParticipantHasPenalties()
    {
        // Arrange
        var game = GameFaker.StartedGame(out _, out var preyIds, StartedAt);
        var endsAt = StartedAt.AddMinutes(10);
        game.ApplyPenalty(preyIds[0], endsAt);

        _repositoryMock
            .Setup(r => r.GetGamesStartedBetweenAsync(It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([game]);

        var query = new ExportGamesQuery(BaseDate, BaseDate.AddDays(1));

        // Act
        var result = await _sut.Handle(query, CancellationToken.None);

        // Assert
        var dto = result[0];
        var preyDto = dto.Participants.Single(p => p.UserId == preyIds[0]);
        Assert.Single(preyDto.Penalties);
        Assert.Equal(endsAt, preyDto.Penalties[0].EndsAt);
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenQueryIsNull()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _sut.Handle(null!, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ShouldPropagateException_WhenRepositoryThrows()
    {
        // Arrange
        _repositoryMock
            .Setup(r => r.GetGamesStartedBetweenAsync(It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database unavailable"));

        var query = new ExportGamesQuery(BaseDate, BaseDate.AddDays(1));

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _sut.Handle(query, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ShouldReturnMultipleGames_WhenSeveralStartedInRange()
    {
        // Arrange
        var game1 = GameFaker.StartedGame(out _, out _, StartedAt);
        var game2 = GameFaker.StartedGame(out _, out _, StartedAt.AddHours(1));

        _repositoryMock
            .Setup(r => r.GetGamesStartedBetweenAsync(It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([game1, game2]);

        var query = new ExportGamesQuery(BaseDate, BaseDate.AddDays(1));

        // Act
        var result = await _sut.Handle(query, CancellationToken.None);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains(result, dto => dto.Id == game1.Id);
        Assert.Contains(result, dto => dto.Id == game2.Id);
    }

    [Fact]
    public async Task Handle_ShouldIncludePreysList_WhenGameHasParticipants()
    {
        // Arrange
        var game = GameFaker.StartedGame(out var hunterId, out var preyIds, StartedAt, playerCount: 4);
        _repositoryMock
            .Setup(r => r.GetGamesStartedBetweenAsync(It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([game]);

        var query = new ExportGamesQuery(BaseDate, BaseDate.AddDays(1));

        // Act
        var result = await _sut.Handle(query, CancellationToken.None);

        // Assert
        var dto = result[0];
        Assert.Equal(preyIds.Count, dto.Preys.Count);
        foreach (var preyId in preyIds)
            Assert.Contains(preyId, dto.Preys);
        Assert.DoesNotContain(hunterId, dto.Preys);
    }
}
