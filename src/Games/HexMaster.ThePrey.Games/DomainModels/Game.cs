namespace HexMaster.ThePrey.Games.DomainModels;

/// <summary>
/// A game of The Prey: a round played by a lobby of players inside a play field, with one hunter
/// chasing the preys. The aggregate root — it owns its lobby, participants, configuration, and
/// enforces every game invariant through behaviour.
/// </summary>
public sealed class Game
{
    /// <summary>Minimum lobby size required to start: one hunter plus at least one prey.</summary>
    public const int MinimumPlayersToStart = 2;

    /// <summary>Reporting interval, in seconds, that applies while a participant has an active penalty.</summary>
    public const int PenaltyReportingIntervalSeconds = 10;

    private readonly List<LobbyPlayer> _lobby = [];
    private readonly List<GameParticipant> _participants = [];

    public Guid Id { get; private set; }
    public Guid PlayfieldId { get; private set; }
    public Guid OwnerUserId { get; private set; }
    public GameStatus Status { get; private set; }
    public GameConfiguration Configuration { get; private set; } = default!;
    public DateTimeOffset? StartedAt { get; private set; }

    public IReadOnlyList<LobbyPlayer> Lobby => _lobby.AsReadOnly();

    /// <summary>The single hunter, or null before the game has started.</summary>
    public GameParticipant? Hunter => _participants.SingleOrDefault(p => p.Role == ParticipantRole.Hunter);

    /// <summary>The preys, empty before the game has started.</summary>
    public IReadOnlyList<GameParticipant> Preys =>
        _participants.Where(p => p.Role == ParticipantRole.Prey).ToList().AsReadOnly();

    private Game() { }

    /// <summary>Creates a new game in the Lobby state with an empty lobby.</summary>
    public static Game Create(Guid ownerUserId, Guid playfieldId, GameConfiguration configuration)
    {
        if (ownerUserId == Guid.Empty)
            throw new ArgumentException("A game requires a non-empty owner identifier.", nameof(ownerUserId));

        if (playfieldId == Guid.Empty)
            throw new ArgumentException("A game requires a non-empty play-field identifier.", nameof(playfieldId));

        ArgumentNullException.ThrowIfNull(configuration);

        return new Game
        {
            Id = Guid.NewGuid(),
            OwnerUserId = ownerUserId,
            PlayfieldId = playfieldId,
            Configuration = configuration,
            Status = GameStatus.Lobby
        };
    }

    /// <summary>Reconstructs a previously-persisted game. Intended only for data adapters.</summary>
    public static Game Rehydrate(
        Guid id,
        Guid ownerUserId,
        Guid playfieldId,
        GameStatus status,
        GameConfiguration configuration,
        DateTimeOffset? startedAt,
        IEnumerable<LobbyPlayer> lobby,
        IEnumerable<GameParticipant> participants)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var game = new Game
        {
            Id = id,
            OwnerUserId = ownerUserId,
            PlayfieldId = playfieldId,
            Status = status,
            Configuration = configuration,
            StartedAt = startedAt
        };
        game._lobby.AddRange(lobby);
        game._participants.AddRange(participants);
        return game;
    }

    /// <summary>Adds a player to the lobby. Only allowed before the game starts; rejects duplicates.</summary>
    public void JoinLobby(LobbyPlayer player)
    {
        ArgumentNullException.ThrowIfNull(player);

        if (Status != GameStatus.Lobby)
            throw new InvalidOperationException("Players can only join a game that is in the lobby.");

        if (_lobby.Any(p => p.UserId == player.UserId))
            throw new InvalidOperationException("This player is already in the lobby.");

        _lobby.Add(player);
    }

    /// <summary>
    /// Starts the game: designates the hunter, turns every other lobby member into a prey, records the
    /// start time, and transitions to InProgress.
    /// </summary>
    public void Start(Guid hunterUserId, DateTimeOffset startedAt)
    {
        if (Status != GameStatus.Lobby)
            throw new InvalidOperationException("Only a game in the lobby can be started.");

        if (_lobby.Count < MinimumPlayersToStart)
            throw new InvalidOperationException($"A game requires at least {MinimumPlayersToStart} players to start.");

        if (_lobby.All(p => p.UserId != hunterUserId))
            throw new InvalidOperationException("The designated hunter must be a member of the lobby.");

        _participants.Clear();
        foreach (var player in _lobby)
        {
            var role = player.UserId == hunterUserId ? ParticipantRole.Hunter : ParticipantRole.Prey;
            _participants.Add(GameParticipant.Create(player.UserId, role));
        }

        StartedAt = startedAt;
        Status = GameStatus.InProgress;
    }

    /// <summary>Records a GPS location for a participant of an in-progress game.</summary>
    public void RecordLocation(Guid userId, GpsCoordinate coordinate, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(coordinate);

        if (Status != GameStatus.InProgress)
            throw new InvalidOperationException("Locations can only be recorded while the game is in progress.");

        var participant = FindParticipant(userId)
            ?? throw new InvalidOperationException("Only a participant of the game can record a location.");

        participant.RecordLocation(LocationReading.Create(coordinate, at));
    }

    /// <summary>Applies a penalty, expiring at <paramref name="endsAt"/>, to a participant.</summary>
    public void ApplyPenalty(Guid userId, DateTimeOffset endsAt)
    {
        if (Status != GameStatus.InProgress)
            throw new InvalidOperationException("Penalties can only be applied while the game is in progress.");

        var participant = FindParticipant(userId)
            ?? throw new InvalidOperationException("Only a participant of the game can be penalised.");

        participant.ApplyPenalty(Penalty.Create(endsAt));
    }

    /// <summary>Transitions an in-progress game to Completed.</summary>
    public void Complete(DateTimeOffset at)
    {
        if (Status != GameStatus.InProgress)
            throw new InvalidOperationException("Only an in-progress game can be completed.");

        Status = GameStatus.Completed;
    }

    /// <summary>The moment the game is scheduled to end (start time plus the configured duration).</summary>
    public DateTimeOffset? ScheduledEndAt =>
        StartedAt is { } started ? started.AddMinutes(Configuration.GameDuration) : null;

    /// <summary>True when <paramref name="now"/> falls within the last <c>FinalStageDuration</c> minutes of the game.</summary>
    public bool IsInFinalStage(DateTimeOffset now)
    {
        if (Status != GameStatus.InProgress || ScheduledEndAt is not { } end)
            return false;

        var finalStageStart = end.AddMinutes(-Configuration.FinalStageDuration);
        return now >= finalStageStart && now < end;
    }

    /// <summary>True once the hunter head-start has elapsed (start time plus <c>HunterDelayTime</c> minutes).</summary>
    public bool AreHuntersAllowedToMove(DateTimeOffset now)
    {
        if (StartedAt is not { } started)
            return false;

        return now >= started.AddMinutes(Configuration.HunterDelayTime);
    }

    /// <summary>
    /// The interval, in seconds, at which the given participant must report its location at <paramref name="now"/>:
    /// 10 seconds while penalised (takes precedence), otherwise the final-stage interval during the final stage,
    /// otherwise the default interval.
    /// </summary>
    public int ReportingIntervalFor(Guid userId, DateTimeOffset now)
    {
        var participant = FindParticipant(userId)
            ?? throw new InvalidOperationException("Only a participant of the game has a reporting interval.");

        return participant.HasActivePenalty(now)
            ? PenaltyReportingIntervalSeconds
            : RegularReportingIntervalAt(now);
    }

    /// <summary>
    /// The regular (penalty-agnostic) reporting interval, in seconds, at <paramref name="now"/>:
    /// the final-stage interval during the final stage, otherwise the default interval.
    /// </summary>
    public int RegularReportingIntervalAt(DateTimeOffset now) =>
        IsInFinalStage(now)
            ? Configuration.FinalLocationInterval
            : Configuration.DefaultLocationInterval;

    /// <summary>
    /// The moment the given participant's active penalty expires, or null when no penalty is active
    /// at <paramref name="now"/>. When multiple penalties overlap, the latest expiry wins.
    /// </summary>
    public DateTimeOffset? ActivePenaltyEndsAtFor(Guid userId, DateTimeOffset now)
    {
        var participant = FindParticipant(userId)
            ?? throw new InvalidOperationException("Only a participant of the game can have a penalty.");

        return participant.ActivePenaltyEndsAt(now);
    }

    /// <summary>True when the given user is the hunter or one of the preys.</summary>
    public bool IsParticipant(Guid userId) => _participants.Any(p => p.UserId == userId);

    /// <summary>True when the given user owns the game or has joined its lobby.</summary>
    public bool IsVisibleTo(Guid userId) =>
        OwnerUserId == userId || _lobby.Any(p => p.UserId == userId);

    private GameParticipant? FindParticipant(Guid userId) =>
        _participants.FirstOrDefault(p => p.UserId == userId);
}
