using HexMaster.ThePrey.Games.Abstractions.DataTransferObjects;
using HexMaster.ThePrey.Games.DomainModels;
using HexMaster.ThePrey.Games.Features.GetTagCandidates;
using HexMaster.ThePrey.Games.Tests.Factories;
using Moq;

namespace HexMaster.ThePrey.Games.Tests.Features.GetTagCandidates;

public sealed class GetTagCandidatesQueryHandlerTests
{
    private static readonly DateTimeOffset Start = new(2026, 6, 9, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Now = Start.AddMinutes(10);

    // Hunter anchor: 52.1° N, 5.1° E
    private static readonly GpsCoordinate HunterCoord = GpsCoordinate.Create(52.1, 5.1);

    // ~30 m north of hunter: +0.00027° lat ≈ 30 m — within 50 m range
    private static readonly GpsCoordinate NearCoord = GpsCoordinate.Create(52.10027, 5.1);

    // ~89 m north of hunter: +0.0008° lat ≈ 89 m — beyond 50 m range
    private static readonly GpsCoordinate FarCoord = GpsCoordinate.Create(52.1008, 5.1);

    // ~44 m north: +0.000396° lat — safely ≤ 50 m, used for boundary "included" check
    private static readonly GpsCoordinate BoundaryInsideCoord = GpsCoordinate.Create(52.10040, 5.1);

    // ~56 m north: +0.000503° lat — just over 50 m, used for boundary "excluded" check
    private static readonly GpsCoordinate BoundaryOutsideCoord = GpsCoordinate.Create(52.10056, 5.1);

    private readonly Mock<IGameRepository> _repository = new();
    private readonly GetTagCandidatesQueryHandler _handler;

    public GetTagCandidatesQueryHandlerTests()
    {
        _handler = new GetTagCandidatesQueryHandler(_repository.Object);
    }

    // ── Unknown game ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ShouldReturnNull_WhenGameNotFound()
    {
        _repository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Game?)null);

        var result = await _handler.Handle(
            new GetTagCandidatesQuery(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        Assert.Null(result);
    }

    // ── Authorization ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ShouldThrowUnauthorized_WhenCallerIsNotHunter()
    {
        var game = GameFaker.StartedGame(out _, out var preyIds, Start);
        _repository.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);

        // A prey (not the hunter) calls the query.
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _handler.Handle(new GetTagCandidatesQuery(game.Id, preyIds[0]), CancellationToken.None));
    }

    // ── Happy path: candidates within range ───────────────────────────────────

    [Fact]
    public async Task Handle_ShouldReturnOnlyPreysWithinRange_WhenSomeAreNearAndSomeFar()
    {
        // 1 hunter + 2 preys: one near (~30 m), one far (~89 m).
        var game = GameFaker.StartedGame(out var hunterId, out var preyIds, Start, playerCount: 3);
        game.RecordLocation(hunterId, HunterCoord, Now);
        game.RecordLocation(preyIds[0], NearCoord, Now);  // ~30 m — within 50 m
        game.RecordLocation(preyIds[1], FarCoord, Now);   // ~89 m — beyond 50 m
        _repository.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);

        var result = await _handler.Handle(new GetTagCandidatesQuery(game.Id, hunterId), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(Game.TagRangeMeters, result!.RangeMeters);
        Assert.Single(result.Candidates);
        Assert.Equal(preyIds[0], result.Candidates[0].UserId);
        Assert.InRange(result.Candidates[0].DistanceMeters, 0, 50);
    }

    [Fact]
    public async Task Handle_ShouldReturnCandidate_WhenPreyIsWithinBoundary()
    {
        // Prey at ~44 m — comfortably inside the 50 m boundary.
        var game = GameFaker.StartedGame(out var hunterId, out var preyIds, Start, playerCount: 2);
        game.RecordLocation(hunterId, HunterCoord, Now);
        game.RecordLocation(preyIds[0], BoundaryInsideCoord, Now);
        _repository.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);

        var result = await _handler.Handle(new GetTagCandidatesQuery(game.Id, hunterId), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Single(result!.Candidates);
        Assert.True(result.Candidates[0].DistanceMeters <= Game.TagRangeMeters);
    }

    [Fact]
    public async Task Handle_ShouldExcludeCandidate_WhenPreyIsJustBeyondBoundary()
    {
        // Prey at ~56 m — just outside the 50 m boundary.
        var game = GameFaker.StartedGame(out var hunterId, out var preyIds, Start, playerCount: 2);
        game.RecordLocation(hunterId, HunterCoord, Now);
        game.RecordLocation(preyIds[0], BoundaryOutsideCoord, Now);
        _repository.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);

        var result = await _handler.Handle(new GetTagCandidatesQuery(game.Id, hunterId), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Empty(result!.Candidates);
    }

    // ── Filtered states ───────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ShouldExcludeTaggedPreys_EvenWhenWithinRange()
    {
        var game = GameFaker.StartedGame(out var hunterId, out var preyIds, Start, playerCount: 2);
        game.RecordLocation(hunterId, HunterCoord, Now);
        game.RecordLocation(preyIds[0], NearCoord, Now);
        // Tag the prey so it is no longer Active/Passive.
        game.TagParticipant(hunterId, preyIds[0], Now);
        _repository.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);

        var result = await _handler.Handle(new GetTagCandidatesQuery(game.Id, hunterId), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Empty(result!.Candidates);
    }

    [Fact]
    public async Task Handle_ShouldExcludeOutPreys_EvenWhenWithinRange()
    {
        var game = GameFaker.StartedGame(out var hunterId, out var preyIds, Start, playerCount: 2);
        game.RecordLocation(hunterId, HunterCoord, Now);
        game.RecordLocation(preyIds[0], NearCoord, Start);
        // Drive the prey to Out via timeout (7 min of silence).
        game.ApplyTimeoutTransitions(Start.AddMinutes(8));
        Assert.Equal(PlayerState.Out, game.Participants.Single(p => p.UserId == preyIds[0]).State);
        _repository.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);

        var result = await _handler.Handle(new GetTagCandidatesQuery(game.Id, hunterId), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Empty(result!.Candidates);
    }

    // ── Passive prey is included ──────────────────────────────────────────────

    [Fact]
    public async Task Handle_ShouldIncludePassivePrey_WhenWithinRange()
    {
        var game = GameFaker.StartedGame(out var hunterId, out var preyIds, Start, playerCount: 2);
        game.RecordLocation(hunterId, HunterCoord, Now);
        game.RecordLocation(preyIds[0], NearCoord, Start);
        // Drive the prey to Passive (5 min of silence).
        game.ApplyTimeoutTransitions(Start.AddMinutes(6));
        Assert.Equal(PlayerState.Passive, game.Participants.Single(p => p.UserId == preyIds[0]).State);
        _repository.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);

        var result = await _handler.Handle(new GetTagCandidatesQuery(game.Id, hunterId), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Single(result!.Candidates);
        Assert.Equal("Passive", result.Candidates[0].State);
    }

    // ── Missing locations ─────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ShouldReturnEmptyCandidates_WhenHunterHasNoLocation()
    {
        // Prey has a location, but the hunter has none — list must be empty.
        var game = GameFaker.StartedGame(out var hunterId, out var preyIds, Start, playerCount: 2);
        game.RecordLocation(preyIds[0], NearCoord, Now);
        // No RecordLocation for hunterId.
        _repository.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);

        var result = await _handler.Handle(new GetTagCandidatesQuery(game.Id, hunterId), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Empty(result!.Candidates);
    }

    [Fact]
    public async Task Handle_ShouldExcludePrey_WhenPreyHasNoLocation()
    {
        // Hunter has a location; this prey has none — must be excluded.
        var game = GameFaker.StartedGame(out var hunterId, out var preyIds, Start, playerCount: 2);
        game.RecordLocation(hunterId, HunterCoord, Now);
        // No RecordLocation for preyIds[0].
        _repository.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);

        var result = await _handler.Handle(new GetTagCandidatesQuery(game.Id, hunterId), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Empty(result!.Candidates);
    }

    // ── Most-recent-reading wins ──────────────────────────────────────────────

    [Fact]
    public async Task Handle_ShouldIncludePrey_WhenMostRecentReadingIsNear_AndOldReadingWasFar()
    {
        // Old reading: far away. New reading: near. Should be included.
        var game = GameFaker.StartedGame(out var hunterId, out var preyIds, Start, playerCount: 2);
        game.RecordLocation(hunterId, HunterCoord, Now);
        game.RecordLocation(preyIds[0], FarCoord, Start);           // old, far
        game.RecordLocation(preyIds[0], NearCoord, Start.AddMinutes(5)); // newer, near
        _repository.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);

        var result = await _handler.Handle(new GetTagCandidatesQuery(game.Id, hunterId), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Single(result!.Candidates);
        Assert.InRange(result.Candidates[0].DistanceMeters, 0, 50);
    }

    [Fact]
    public async Task Handle_ShouldExcludePrey_WhenMostRecentReadingIsFar_AndOldReadingWasNear()
    {
        // Old reading: near. New reading: far. Should be excluded.
        var game = GameFaker.StartedGame(out var hunterId, out var preyIds, Start, playerCount: 2);
        game.RecordLocation(hunterId, HunterCoord, Now);
        game.RecordLocation(preyIds[0], NearCoord, Start);               // old, near
        game.RecordLocation(preyIds[0], FarCoord, Start.AddMinutes(5));  // newer, far
        _repository.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);

        var result = await _handler.Handle(new GetTagCandidatesQuery(game.Id, hunterId), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Empty(result!.Candidates);
    }

    // ── DTO field mapping ─────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ShouldMapCandidateFields_Correctly()
    {
        var expectedName = "Callsign Test";
        var game = GameFaker.LobbyGame();
        var preyId = Guid.NewGuid();
        var hunterId = Guid.NewGuid();
        game.JoinLobby(GameFaker.Player(hunterId, "Hunter"));
        game.JoinLobby(GameFaker.Player(preyId, expectedName));
        game.SetReady(hunterId);
        game.SetReady(preyId);
        game.DesignateHunter(hunterId);
        game.Arm(hunterId);
        game.BeginPlay(Start);

        game.RecordLocation(hunterId, HunterCoord, Now);
        game.RecordLocation(preyId, NearCoord, Now);
        _repository.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);

        var result = await _handler.Handle(new GetTagCandidatesQuery(game.Id, hunterId), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Single(result!.Candidates);
        var candidate = result.Candidates[0];
        Assert.Equal(preyId, candidate.UserId);
        Assert.Equal(expectedName, candidate.Callsign);
        Assert.Equal("Active", candidate.State);
        Assert.InRange(candidate.DistanceMeters, 0, 50);
    }
}
