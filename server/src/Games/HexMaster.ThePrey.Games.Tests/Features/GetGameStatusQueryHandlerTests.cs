using HexMaster.ThePrey.Games.Abstractions.DataTransferObjects;
using HexMaster.ThePrey.Games.DomainModels;
using HexMaster.ThePrey.Games.Features.GetGameStatus;
using HexMaster.ThePrey.Games.Tests.Factories;
using Moq;

namespace HexMaster.ThePrey.Games.Tests.Features;

public sealed class GetGameStatusQueryHandlerTests
{
    private readonly Mock<IGameRepository> _repositoryMock = new();
    private readonly Mock<IPlayfieldInfoProvider> _playfieldsMock = new();
    private readonly GetGameStatusQueryHandler _sut;

    public GetGameStatusQueryHandlerTests()
    {
        _sut = new GetGameStatusQueryHandler(_repositoryMock.Object, _playfieldsMock.Object);
    }

    [Fact]
    public async Task Handle_ShouldReturnNull_WhenGameNotFound()
    {
        _repositoryMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Game?)null);

        var result = await _sut.Handle(new GetGameStatusQuery(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenUserIsNotParticipant()
    {
        var game = GameFaker.StartedGame(out _, out _, DateTimeOffset.UtcNow.AddMinutes(-5));
        var nonParticipantId = Guid.NewGuid();

        _repositoryMock
            .Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _sut.Handle(new GetGameStatusQuery(game.Id, nonParticipantId), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenGameIsNotInProgress()
    {
        var startedAt = DateTimeOffset.UtcNow.AddMinutes(-5);
        var game = GameFaker.StartedGame(out var hunterId, out _, startedAt);
        game.Complete(DateTimeOffset.UtcNow);

        _repositoryMock
            .Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _sut.Handle(new GetGameStatusQuery(game.Id, hunterId), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ShouldReturnStatus_WhenParticipantQueriesInProgressGame()
    {
        var startedAt = DateTimeOffset.UtcNow.AddMinutes(-5);
        var game = GameFaker.StartedGame(out var hunterId, out var preyIds, startedAt);

        var playfieldInfo = new PlayfieldInfo("Test Field", [new GpsCoordinateDto(1.0, 2.0), new GpsCoordinateDto(2.0, 3.0), new GpsCoordinateDto(3.0, 1.0)]);

        _repositoryMock
            .Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        _playfieldsMock
            .Setup(p => p.GetAsync(game.PlayfieldId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(playfieldInfo);

        var result = await _sut.Handle(new GetGameStatusQuery(game.Id, hunterId), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(game.Id, result!.GameId);
        Assert.Equal("Test Field", result.PlayfieldName);
        Assert.Equal(3, result.PlayfieldCoordinates.Count);
        Assert.Equal(hunterId, result.HunterUserId);
        Assert.Equal(game.Participants.Count, result.Participants.Count);
        Assert.True(result.GameDurationLeft > 0);
        Assert.False(result.IsEndgame);
    }

    [Fact]
    public async Task Handle_ShouldReturnNullPlayfieldInfo_WhenPlayfieldNotFound()
    {
        var game = GameFaker.StartedGame(out var hunterId, out _, DateTimeOffset.UtcNow.AddMinutes(-5));

        _repositoryMock
            .Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        _playfieldsMock
            .Setup(p => p.GetAsync(game.PlayfieldId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PlayfieldInfo?)null);

        var result = await _sut.Handle(new GetGameStatusQuery(game.Id, hunterId), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(string.Empty, result!.PlayfieldName);
        Assert.Empty(result.PlayfieldCoordinates);
    }

    [Fact]
    public async Task Handle_ShouldThrowNull_WhenQueryIsNull()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _sut.Handle(null!, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ShouldSetGameDurationLeftToZero_WhenGameExpired()
    {
        var startedAt = DateTimeOffset.UtcNow.AddMinutes(-200);
        var game = GameFaker.StartedGame(out var hunterId, out _, startedAt);

        _repositoryMock
            .Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        _playfieldsMock
            .Setup(p => p.GetAsync(game.PlayfieldId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PlayfieldInfo?)null);

        var result = await _sut.Handle(new GetGameStatusQuery(game.Id, hunterId), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(0, result!.GameDurationLeft);
    }

    [Fact]
    public async Task Handle_ShouldReturnPreyStatus_WhenPreyQueriesGame()
    {
        var game = GameFaker.StartedGame(out var hunterId, out var preyIds, DateTimeOffset.UtcNow.AddMinutes(-5));
        var preyId = preyIds[0];

        _repositoryMock
            .Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        _playfieldsMock
            .Setup(p => p.GetAsync(game.PlayfieldId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PlayfieldInfo?)null);

        var result = await _sut.Handle(new GetGameStatusQuery(game.Id, preyId), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(game.Id, result!.GameId);
        Assert.Equal(hunterId, result.HunterUserId);
        Assert.NotEmpty(result.Participants);
    }

    [Fact]
    public async Task Handle_ShouldSetHasActivePenaltyTrue_WhenParticipantHasPenalty()
    {
        var startedAt = DateTimeOffset.UtcNow.AddMinutes(-5);
        var game = GameFaker.StartedGame(out _, out var preyIds, startedAt);
        var preyId = preyIds[0];
        game.ApplyPenalty(preyId, DateTimeOffset.UtcNow.AddSeconds(30));

        _repositoryMock
            .Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        _playfieldsMock
            .Setup(p => p.GetAsync(game.PlayfieldId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PlayfieldInfo?)null);

        var result = await _sut.Handle(new GetGameStatusQuery(game.Id, preyId), CancellationToken.None);

        Assert.NotNull(result);
        var preyStatus = result!.Participants.Single(p => p.UserId == preyId);
        Assert.True(preyStatus.HasActivePenalty);
    }

    [Fact]
    public async Task Handle_ShouldSetHasActivePenaltyFalse_WhenNoPenaltyActive()
    {
        var startedAt = DateTimeOffset.UtcNow.AddMinutes(-5);
        var game = GameFaker.StartedGame(out var hunterId, out _, startedAt);

        _repositoryMock
            .Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        _playfieldsMock
            .Setup(p => p.GetAsync(game.PlayfieldId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PlayfieldInfo?)null);

        var result = await _sut.Handle(new GetGameStatusQuery(game.Id, hunterId), CancellationToken.None);

        Assert.NotNull(result);
        var hunterStatus = result!.Participants.Single(p => p.UserId == hunterId);
        Assert.False(hunterStatus.HasActivePenalty);
    }
}
