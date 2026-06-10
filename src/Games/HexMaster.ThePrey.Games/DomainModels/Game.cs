namespace HexMaster.ThePrey.Games.DomainModels;

/// <summary>
/// A game of The Prey: a round played by a set of players inside a play field, with one hunter
/// chasing the preys. The aggregate root — it owns its participants, configuration, and enforces
/// every game invariant through behaviour.
/// </summary>
public sealed class Game
{
    /// <summary>Minimum participants required to start: one hunter plus at least one prey.</summary>
    public const int MinimumPlayersToStart = 2;

    /// <summary>Maximum number of players that can join.</summary>
    public const int MaxLobbySize = 16;

    /// <summary>Length of the shareable game code: exactly this many decimal digits.</summary>
    public const int GameCodeLength = 4;

    /// <summary>How many hours after creation a game record is eligible for hard deletion.</summary>
    public const int CleanupWindowHours = 48;

    /// <summary>Reporting interval, in seconds, that applies while a participant has an active penalty.</summary>
    public const int PenaltyReportingIntervalSeconds = 10;

    private readonly List<GameParticipant> _participants = [];

    public Guid Id { get; private set; }

    /// <summary>The shareable code players use to find this game: exactly 4 decimal digits.</summary>
    public string GameCode { get; private set; } = default!;

    public Guid PlayfieldId { get; private set; }
    public Guid OwnerUserId { get; private set; }
    public GameStatus Status { get; private set; }
    public GameConfiguration Configuration { get; private set; } = default!;
    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? EndsAt { get; private set; }
    public DateTimeOffset CleanUpAfter { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public GameOutcome Outcome { get; private set; }

    /// <summary>
    /// The current hunter's UserId. In Lobby state this is the pre-designated hunter (null until designated).
    /// In InProgress state this is the active hunter.
    /// </summary>
    public Guid? HunterUserId { get; private set; }

    /// <summary>All participants (lobby members and in-progress players share this single collection).</summary>
    public IReadOnlyList<GameParticipant> Participants => _participants.AsReadOnly();

    /// <summary>
    /// Derived list of prey UserIds: every participant except the hunter.
    /// Only meaningful when <see cref="Status"/> is InProgress.
    /// </summary>
    public IReadOnlyList<Guid> Preys =>
        _participants.Where(p => p.UserId != HunterUserId).Select(p => p.UserId).ToList();

    private Game() { }

    /// <summary>Creates a new game in the Lobby state with an empty participants list.</summary>
    public static Game Create(Guid ownerUserId, Guid playfieldId, string gameCode, GameConfiguration configuration)
    {
        if (ownerUserId == Guid.Empty)
            throw new ArgumentException("A game requires a non-empty owner identifier.", nameof(ownerUserId));

        if (playfieldId == Guid.Empty)
            throw new ArgumentException("A game requires a non-empty play-field identifier.", nameof(playfieldId));

        ValidateGameCode(gameCode);
        ArgumentNullException.ThrowIfNull(configuration);

        var now = DateTimeOffset.UtcNow;
        return new Game
        {
            Id = Guid.NewGuid(),
            GameCode = gameCode,
            OwnerUserId = ownerUserId,
            PlayfieldId = playfieldId,
            Configuration = configuration,
            Status = GameStatus.Lobby,
            CreatedAt = now,
            CleanUpAfter = now.AddHours(CleanupWindowHours)
        };
    }

    /// <summary>Reconstructs a previously-persisted game. Intended only for data adapters.</summary>
    public static Game Rehydrate(
        Guid id,
        string gameCode,
        Guid ownerUserId,
        Guid playfieldId,
        GameStatus status,
        GameConfiguration configuration,
        DateTimeOffset? startedAt,
        IEnumerable<GameParticipant> participants,
        Guid? hunterUserId = null,
        DateTimeOffset createdAt = default,
        DateTimeOffset? endsAt = null,
        DateTimeOffset cleanUpAfter = default,
        DateTimeOffset? completedAt = null,
        GameOutcome outcome = GameOutcome.Undecided)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var game = new Game
        {
            Id = id,
            GameCode = gameCode,
            OwnerUserId = ownerUserId,
            PlayfieldId = playfieldId,
            Status = status,
            Configuration = configuration,
            StartedAt = startedAt,
            HunterUserId = hunterUserId,
            CreatedAt = createdAt,
            EndsAt = endsAt,
            CleanUpAfter = cleanUpAfter,
            CompletedAt = completedAt,
            Outcome = outcome
        };
        game._participants.AddRange(participants);
        return game;
    }

    /// <summary>Adds a player to the participants list. Only allowed before the game starts; rejects duplicates and a full lobby.</summary>
    public void JoinLobby(GameParticipant participant)
    {
        ArgumentNullException.ThrowIfNull(participant);

        if (Status != GameStatus.Lobby)
            throw new GameNotJoinableException();

        if (_participants.Any(p => p.UserId == participant.UserId))
            throw new PlayerAlreadyInLobbyException();

        if (_participants.Count >= MaxLobbySize)
            throw new LobbyFullException(MaxLobbySize);

        _participants.Add(participant);
    }

    /// <summary>
    /// Pre-designates a participant as the hunter before the game starts.
    /// Only allowed while in Lobby state; the user must already be a participant.
    /// </summary>
    public void DesignateHunter(Guid userId)
    {
        if (Status != GameStatus.Lobby)
            throw new InvalidOperationException("Hunter can only be designated while the game is in the lobby.");
        if (_participants.All(p => p.UserId != userId))
            throw new ArgumentException("The designated hunter must be a participant.", nameof(userId));
        HunterUserId = userId;
    }

    /// <summary>
    /// Removes a player from the participants list. Only allowed while in Lobby state.
    /// Clears the hunter designation if the removed player was designated.
    /// </summary>
    public void RemoveLobbyPlayer(Guid userId)
    {
        if (Status != GameStatus.Lobby)
            throw new InvalidOperationException("Players can only be removed while the game is in the lobby.");
        var player = _participants.FirstOrDefault(p => p.UserId == userId)
            ?? throw new ArgumentException("This player is not in the lobby.", nameof(userId));
        _participants.Remove(player);
        if (HunterUserId == userId)
            HunterUserId = null;
    }

    /// <summary>
    /// Updates game settings and resets the ready flag for all non-owner participants.
    /// Only allowed while in Lobby state.
    /// </summary>
    public void UpdateSettings(GameConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (Status != GameStatus.Lobby)
            throw new InvalidOperationException("Settings can only be updated while the game is in the lobby.");
        Configuration = config;
        foreach (var p in _participants.Where(p => p.UserId != OwnerUserId))
            p.SetReady(false);
    }

    /// <summary>
    /// Marks a participant as ready. No-op for the owner. Throws if the user is not a participant.
    /// </summary>
    public void SetReady(Guid userId)
    {
        if (userId == OwnerUserId) return;
        var participant = FindParticipant(userId)
            ?? throw new ArgumentException("This player is not in the lobby.", nameof(userId));
        participant.SetReady(true);
    }

    /// <summary>
    /// Whether every precondition of <see cref="Start"/> is currently met: the game is still in the
    /// lobby, has at least <see cref="MinimumPlayersToStart"/> players, has a designated hunter who is a
    /// participant, and every non-owner player has readied up.
    /// </summary>
    public bool IsReadyToStart =>
        Status == GameStatus.Lobby
        && _participants.Count >= MinimumPlayersToStart
        && HunterUserId is { } hunterId
        && _participants.Any(p => p.UserId == hunterId)
        && _participants.All(p => p.UserId == OwnerUserId || p.IsReady);

    /// <summary>
    /// Starts the game. Does NOT recreate participants — the existing participants (lobby members)
    /// become the in-progress roster. Sets the hunter, records timing, and transitions to InProgress.
    /// </summary>
    public void Start(Guid hunterUserId, DateTimeOffset startedAt)
    {
        if (Status != GameStatus.Lobby)
            throw new InvalidOperationException("Only a game in the lobby can be started.");

        if (_participants.Count < MinimumPlayersToStart)
            throw new InvalidOperationException($"A game requires at least {MinimumPlayersToStart} players to start.");

        if (_participants.All(p => p.UserId != hunterUserId))
            throw new InvalidOperationException("The designated hunter must be a participant.");

        if (_participants.Any(p => p.UserId != OwnerUserId && !p.IsReady))
            throw new InvalidOperationException("All players must be ready before the game can start.");

        HunterUserId = hunterUserId;
        StartedAt = startedAt;
        EndsAt = startedAt.AddMinutes(Configuration.GameDuration);
        Status = GameStatus.InProgress;
    }

    /// <summary>
    /// Reassigns the hunter role to an existing prey of an in-progress game.
    /// The game stays InProgress; the previous hunter becomes a prey (derived, no role field).
    /// </summary>
    public void SetHunter(Guid newHunterUserId)
    {
        if (Status != GameStatus.InProgress)
            throw new InvalidOperationException("The hunter can only be changed while the game is in progress.");

        if (HunterUserId is null)
            throw new InvalidOperationException("An in-progress game must have a hunter.");

        if (HunterUserId == newHunterUserId)
            throw new ArgumentException("This player is already the hunter.", nameof(newHunterUserId));

        // New hunter must be an existing participant who is currently a prey (not the hunter).
        if (_participants.All(p => p.UserId != newHunterUserId))
            throw new ArgumentException("The new hunter must be an existing prey of the game.", nameof(newHunterUserId));

        if (newHunterUserId == HunterUserId)
            throw new ArgumentException("The new hunter must be an existing prey of the game.", nameof(newHunterUserId));

        HunterUserId = newHunterUserId;
    }

    /// <summary>
    /// Records a GPS location for a participant of an in-progress game and activates their state
    /// (no-op when the participant is Out or Tagged). Returns the participant's previous state.
    /// </summary>
    public PlayerState RecordLocation(Guid userId, GpsCoordinate coordinate, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(coordinate);

        if (Status != GameStatus.InProgress)
            throw new InvalidOperationException("Locations can only be recorded while the game is in progress.");

        var participant = FindParticipant(userId)
            ?? throw new InvalidOperationException("Only a participant of the game can record a location.");

        participant.RecordLocation(LocationReading.Create(coordinate, at));
        return participant.ActivateIfAllowed(at);
    }

    /// <summary>
    /// Applies timeout-based state transitions to all prey participants. Returns a list of
    /// (userId, newState) pairs for every participant whose state changed.
    /// </summary>
    public IReadOnlyList<(Guid UserId, PlayerState NewState)> ApplyTimeoutTransitions(DateTimeOffset now)
    {
        var changes = new List<(Guid, PlayerState)>();
        foreach (var participant in _participants.Where(p => p.UserId != HunterUserId))
        {
            if (participant.TryTransitionByTimeout(now, out var newState))
                changes.Add((participant.UserId, newState));
        }
        return changes;
    }

    /// <summary>
    /// Marks a prey participant as Tagged. The caller must be the hunter; the target must be a prey
    /// in Active or Passive state; the game must be InProgress.
    /// </summary>
    public void TagParticipant(Guid callerId, Guid targetUserId)
    {
        if (Status != GameStatus.InProgress)
            throw new InvalidOperationException("Players can only be tagged while the game is in progress.");

        if (HunterUserId != callerId)
            throw new UnauthorizedAccessException("Only the hunter can tag preys.");

        // Target must be a participant and must NOT be the hunter.
        var target = _participants.FirstOrDefault(p => p.UserId == targetUserId && p.UserId != HunterUserId)
            ?? throw new ArgumentException("The target participant is not a prey of this game.", nameof(targetUserId));

        target.Tag();
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

    /// <summary>Transitions an in-progress game to Completed and records the outcome.</summary>
    public void Complete(DateTimeOffset at)
    {
        if (Status != GameStatus.InProgress)
            throw new InvalidOperationException("Only an in-progress game can be completed.");

        ApplyCompletion(at, ComputeOutcome());
    }

    /// <summary>
    /// Ends the game on behalf of its owner. Allowed from Lobby (cancel) and InProgress (force-complete).
    /// Throws when the game is already Completed.
    /// </summary>
    public void EndByOwner(DateTimeOffset now)
    {
        if (Status == GameStatus.Completed)
            throw new InvalidOperationException("The game has already been completed.");

        var outcome = Status == GameStatus.Lobby ? GameOutcome.Cancelled : ComputeOutcome();
        ApplyCompletion(now, outcome);
    }

    private void ApplyCompletion(DateTimeOffset at, GameOutcome outcome)
    {
        Status = GameStatus.Completed;
        CompletedAt = at;
        Outcome = outcome;
    }

    /// <summary>
    /// Computes the outcome of an in-progress game based on the current participant states.
    /// Hunters win when every prey is Tagged or Out; preys win when at least one survives.
    /// </summary>
    private GameOutcome ComputeOutcome()
    {
        var preys = _participants.Where(p => p.UserId != HunterUserId).ToList();
        if (preys.Count == 0) return GameOutcome.Undecided;

        return preys.All(p => p.State is PlayerState.Tagged or PlayerState.Out)
            ? GameOutcome.HuntersWin
            : GameOutcome.PreysWin;
    }

    /// <summary>
    /// Marks a prey participant as Out (forfeit). Allowed while InProgress.
    /// Throws when the game is not InProgress or the user is not an active prey.
    /// </summary>
    public void Forfeit(Guid userId)
    {
        if (Status != GameStatus.InProgress)
            throw new InvalidOperationException("A participant can only forfeit while the game is in progress.");

        var participant = FindParticipant(userId)
            ?? throw new ArgumentException("This user is not a participant of the game.", nameof(userId));

        if (participant.UserId == HunterUserId)
            throw new InvalidOperationException("Only a prey participant can forfeit.");

        participant.ForfeitOut();
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
    /// The interval, in seconds, at which the given participant must report its location at <paramref name="now"/>.
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
    /// The regular (penalty-agnostic) reporting interval, in seconds, at <paramref name="now"/>.
    /// </summary>
    public int RegularReportingIntervalAt(DateTimeOffset now) =>
        IsInFinalStage(now)
            ? Configuration.FinalLocationInterval
            : Configuration.DefaultLocationInterval;

    /// <summary>
    /// The moment the given participant's active penalty expires, or null when no penalty is active.
    /// </summary>
    public DateTimeOffset? ActivePenaltyEndsAtFor(Guid userId, DateTimeOffset now)
    {
        var participant = FindParticipant(userId)
            ?? throw new InvalidOperationException("Only a participant of the game can have a penalty.");

        return participant.ActivePenaltyEndsAt(now);
    }

    /// <summary>True when the given user is a participant of the game.</summary>
    public bool IsParticipant(Guid userId) => _participants.Any(p => p.UserId == userId);

    /// <summary>True when the given user owns the game or is a participant.</summary>
    public bool IsVisibleTo(Guid userId) =>
        OwnerUserId == userId || IsParticipant(userId);

    private GameParticipant? FindParticipant(Guid userId) =>
        _participants.FirstOrDefault(p => p.UserId == userId);

    private static void ValidateGameCode(string gameCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameCode);

        if (gameCode.Length != GameCodeLength || !gameCode.All(char.IsAsciiDigit))
            throw new ArgumentException(
                $"A game code must consist of exactly {GameCodeLength} decimal digits.", nameof(gameCode));
    }
}
