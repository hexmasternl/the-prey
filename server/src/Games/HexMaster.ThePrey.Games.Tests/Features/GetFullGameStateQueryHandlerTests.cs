using HexMaster.ThePrey.Games.Abstractions.DataTransferObjects;
using HexMaster.ThePrey.Games.DomainModels;
using HexMaster.ThePrey.Games.Features.GetFullGameState;
using HexMaster.ThePrey.Games.Tests.Factories;
using Moq;

namespace HexMaster.ThePrey.Games.Tests.Features;

public sealed class GetFullGameStateQueryHandlerTests
{
    private static readonly DateTimeOffset Start = new(2026, 6, 3, 12, 0, 0, TimeSpan.Zero);

    private static readonly PlayfieldInfo Playfield = new(
        "Test Playfield",
        [new GpsCoordinateDto(1, 1), new GpsCoordinateDto(1, -1), new GpsCoordinateDto(-1, -1)]);

    private readonly Mock<IGameRepository> _repository = new();
    private readonly Mock<IPlayfieldInfoProvider> _playfields = new();

    private GetFullGameStateQueryHandler Sut(DateTimeOffset now) =>
        new(_repository.Object, _playfields.Object, new FixedTimeProvider(now));

    private void SetupGame(Game game)
    {
        _repository.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);
        _playfields.Setup(p => p.GetAsync(game.PlayfieldId, It.IsAny<CancellationToken>())).ReturnsAsync(Playfield);
    }

    [Fact]
    public async Task Handle_ShouldReturnNull_WhenGameNotFound()
    {
        _repository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Game?)null);

        var result = await Sut(Start).Handle(
            new GetFullGameStateQuery(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenCallerCannotSeeTheGame()
    {
        var game = GameFaker.StartedGame(out _, out _, Start);
        SetupGame(game);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            Sut(Start).Handle(new GetFullGameStateQuery(game.Id, Guid.NewGuid()), CancellationToken.None));
    }

    // ---------- in progress: every slice is populated in one response ----------

    [Fact]
    public async Task Handle_ShouldReturnGameStatusAndRoleState_WhenGameIsInProgressAndCallerIsTheHunter()
    {
        var game = GameFaker.StartedGame(out var hunterId, out var preyIds, Start, playerCount: 3);
        GameFaker.RecordColocated(game, Start, hunterId, preyIds[0], preyIds[1]);
        // The sweep is what copies a recorded reading into the broadcast Location the role state reads.
        game.SweepLocations(Start.AddSeconds(30));
        SetupGame(game);

        var result = await Sut(Start.AddMinutes(10)).Handle(
            new GetFullGameStateQuery(game.Id, hunterId), CancellationToken.None);

        Assert.NotNull(result);
        // The game record, with participants.
        Assert.Equal(game.Id, result.Game.Id);
        Assert.Equal(nameof(GameStatus.InProgress), result.Game.Status);
        Assert.Equal(3, result.Game.Participants.Count);
        // The playfield, for drawing the play area.
        Assert.Equal("Test Playfield", result.PlayfieldName);
        Assert.Equal(3, result.PlayfieldCoordinates.Count);
        // The rich status, with remaining time (60-minute game, 10 minutes in → 50 minutes left).
        Assert.NotNull(result.Status);
        Assert.Equal(TimeSpan.FromMinutes(50).TotalSeconds, result.Status.GameDurationLeft);
        Assert.Equal(2, result.Status.PreysLeft);
        // The hunter's role view: every prey's position, and no prey-only distance.
        Assert.NotNull(result.RoleState);
        Assert.Equal(2, result.RoleState.PreyLocations.Count);
        Assert.Null(result.RoleState.HunterDistanceMeters);
        // The server's clock, so the client can correct its countdown for drift.
        Assert.Equal(Start.AddMinutes(10), result.ServerTime);
    }

    [Fact]
    public async Task Handle_ShouldReturnPreyRoleState_WhenGameIsInProgressAndCallerIsAPrey()
    {
        var game = GameFaker.StartedGame(out var hunterId, out var preyIds, Start, playerCount: 3);
        GameFaker.RecordColocated(game, Start, hunterId, preyIds[0]);
        game.SweepLocations(Start.AddSeconds(30));
        SetupGame(game);

        var result = await Sut(Start.AddMinutes(10)).Handle(
            new GetFullGameStateQuery(game.Id, preyIds[0]), CancellationToken.None);

        Assert.NotNull(result);
        Assert.NotNull(result.RoleState);
        // Co-located, so the distance is 0 — and a prey never receives other preys' positions.
        Assert.Equal(0, result.RoleState.HunterDistanceMeters);
        Assert.Empty(result.RoleState.PreyLocations);
    }

    // ---------- outside InProgress: still served, with the two live slices null ----------
    // This is the difference from GET /games/{id}/status, which throws for a non-in-progress game:
    // one endpoint serves the lobby, gameplay, and outcome screens alike.

    [Fact]
    public async Task Handle_ShouldReturnGameWithoutStatusOrRoleState_WhenGameIsStillInTheLobby()
    {
        var game = GameFaker.LobbyGameWithPlayers(2, out var playerIds);
        SetupGame(game);

        var result = await Sut(Start).Handle(
            new GetFullGameStateQuery(game.Id, playerIds[0]), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(nameof(GameStatus.Lobby), result.Game.Status);
        Assert.Equal(2, result.Game.Participants.Count);
        // The playfield is available in the lobby too.
        Assert.Equal("Test Playfield", result.PlayfieldName);
        Assert.Equal(3, result.PlayfieldCoordinates.Count);
        Assert.Null(result.Status);
        Assert.Null(result.RoleState);
    }

    [Fact]
    public async Task Handle_ShouldReturnGameWithOutcome_WhenGameIsCompleted()
    {
        var game = GameFaker.StartedGame(out _, out var preyIds, Start, playerCount: 3);
        game.Complete(Start.AddMinutes(60));
        SetupGame(game);

        var result = await Sut(Start.AddMinutes(61)).Handle(
            new GetFullGameStateQuery(game.Id, preyIds[0]), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(nameof(GameStatus.Completed), result.Game.Status);
        Assert.Equal(nameof(GameOutcome.PreysWin), result.Game.Outcome);
        Assert.NotNull(result.Game.CompletedAt);
        Assert.Null(result.Status);
        Assert.Null(result.RoleState);
    }

    // ---------- per-caller flags ----------

    [Fact]
    public async Task Handle_ShouldStampIsOwnerPlayer_WhenCallerOwnsTheGame()
    {
        var ownerId = Guid.NewGuid();
        var game = GameFaker.LobbyGame(ownerId);
        game.JoinLobby(GameFaker.Player(ownerId));
        SetupGame(game);

        var result = await Sut(Start).Handle(
            new GetFullGameStateQuery(game.Id, ownerId), CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(result.Game.IsOwnerPlayer);
    }

    [Fact]
    public async Task Handle_ShouldReturnEmptyPlayfield_WhenPlayfieldCanNoLongerBeResolved()
    {
        var game = GameFaker.LobbyGameWithPlayers(2, out var playerIds);
        _repository.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);
        _playfields.Setup(p => p.GetAsync(game.PlayfieldId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PlayfieldInfo?)null);

        var result = await Sut(Start).Handle(
            new GetFullGameStateQuery(game.Id, playerIds[0]), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(string.Empty, result.PlayfieldName);
        Assert.Empty(result.PlayfieldCoordinates);
    }

    // ---------- boundary penalties ----------

    [Fact]
    public async Task Handle_ShouldReportBoundaryPenaltiesEnabled_WhenGameWasCreatedWithThemDisabled()
    {
        var config = GameFaker.ValidConfiguration(enablePreyBoundaryPenalties: false, enableHunterBoundaryPenalty: false);
        var game = GameFaker.StartedGame(out var hunterId, out _, Start, configuration: config);
        SetupGame(game);

        var result = await Sut(Start.AddMinutes(1)).Handle(
            new GetFullGameStateQuery(game.Id, hunterId), CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(result.Game.Configuration.EnablePreyBoundaryPenalties);
        Assert.True(result.Game.Configuration.EnableHunterBoundaryPenalty);
    }
}
