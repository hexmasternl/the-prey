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

    /// <summary>Maximum number of players a lobby can hold.</summary>
    public const int MaxLobbySize = 16;

    /// <summary>Length of the shareable game code: exactly this many decimal digits.</summary>
    public const int GameCodeLength = 4;

    /// <summary>How many hours after creation a game record is eligible for hard deletion.</summary>
    public const int CleanupWindowHours = 48;

    /// <summary>Reporting interval, in seconds, that applies while a participant has an active penalty.</summary>
    public const int PenaltyReportingIntervalSeconds = 10;

    private readonly List<LobbyPlayer> _lobby = [];
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

    /// <summary>The lobby player pre-designated as the hunter before the game starts; null until designated.</summary>
    public Guid? DesignatedHunterUserId { get; private set; }

    public IReadOnlyList<LobbyPlayer> Lobby => _lobby.AsReadOnly();

    /// <summary>The single hunter, or null before the game has started.</summary>
    public GameParticipant? Hunter => _participants.SingleOrDefault(p => p.Role == ParticipantRole.Hunter);

    /// <summary>The preys, empty before the game has started.</summary>
    public IReadOnlyList<GameParticipant> Preys =>
        _participants.Where(p => p.Role == ParticipantRole.Prey).ToList().AsReadOnly();

    private Game() { }

    /// <summary>Creates a new game in the Lobby state with an empty lobby.</summary>
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
        IEnumerable<LobbyPlayer> lobby,
        IEnumerable<GameParticipant> participants,
        Guid? designatedHunterUserId = null,
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
            DesignatedHunterUserId = designatedHunterUserId,
            CreatedAt = createdAt,
            EndsAt = endsAt,
            CleanUpAfter = cleanUpAfter,
            CompletedAt = completedAt,
            Outcome = outcome
        };
        game._lobby.AddRange(lobby);
        game._participants.AddRange(participants);
        return game;
    }

    /// <summary>Adds a player to the lobby. Only allowed before the game starts; rejects duplicates and a full lobby.</summary>
    public void JoinLobby(LobbyPlayer player)
    {
        ArgumentNullException.ThrowIfNull(player);

        if (Status != GameStatus.Lobby)
            throw new InvalidOperationException("Players can only join a game that is in the lobby.");

        if (_lobby.Any(p => p.UserId == player.UserId))
            throw new InvalidOperationException("This player is already in the lobby.");

        if (_lobby.Count >= MaxLobbySize)
            throw new InvalidOperationException($"The lobby is full: a game holds at most {MaxLobbySize} players.");

        _lobby.Add(player);
    }

    /// <summary>
    /// Pre-designates a lobby member as the hunter before the game starts.
    /// Only allowed while in Lobby state; the user must already be in the lobby.
    /// </summary>
    public void DesignateHunter(Guid userId)
    {
        if (Status != GameStatus.Lobby)
            throw new InvalidOperationException("Hunter can only be designated while the game is in the lobby.");
        if (_lobby.All(p => p.UserId != userId))
            throw new ArgumentException("The designated hunter must be a member of the lobby.", nameof(userId));
        DesignatedHunterUserId = userId;
    }

    /// <summary>
    /// Removes a player from the lobby. Only allowed while in Lobby state.
    /// Clears the designated hunter if the removed player was designated.
    /// </summary>
    public void RemoveLobbyPlayer(Guid userId)
    {
        if (Status != GameStatus.Lobby)
            throw new InvalidOperationException("Players can only be removed while the game is in the lobby.");
        var player = _lobby.FirstOrDefault(p => p.UserId == userId)
            ?? throw new ArgumentException("This player is not in the lobby.", nameof(userId));
        _lobby.Remove(player);
        if (DesignatedHunterUserId == userId)
            DesignatedHunterUserId = null;
    }

    /// <summary>
    /// Updates game settings and resets the ready flag for all non-owner lobby members.
    /// Only allowed while in Lobby state.
    /// </summary>
    public void UpdateSettings(GameConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (Status != GameStatus.Lobby)
            throw new InvalidOperationException("Settings can only be updated while the game is in the lobby.");
        Configuration = config;
        for (var i = 0; i < _lobby.Count; i++)
            if (_lobby[i].UserId != OwnerUserId)
                _lobby[i] = _lobby[i].WithReady(false);
    }

    /// <summary>
    /// Marks a lobby player as ready. No-op for the owner. Throws if the user is not in the lobby.
    /// </summary>
    public void SetReady(Guid userId)
    {
        if (userId == OwnerUserId) return;
        var idx = _lobby.FindIndex(p => p.UserId == userId);
        if (idx == -1)
            throw new ArgumentException("This player is not in the lobby.", nameof(userId));
        _lobby[idx] = _lobby[idx].WithReady(true);
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
        EndsAt = startedAt.AddMinutes(Configuration.GameDuration);
        Status = GameStatus.InProgress;
    }

    /// <summary>
    /// Reassigns the hunter role to an existing prey of an in-progress game; the former hunter
    /// becomes a prey. The game stays InProgress.
    /// </summary>
    public void SetHunter(Guid newHunterUserId)
    {
        if (Status != GameStatus.InProgress)
            throw new InvalidOperationException("The hunter can only be changed while the game is in progress.");

        var currentHunter = Hunter
            ?? throw new InvalidOperationException("An in-progress game must have a hunter.");

        if (currentHunter.UserId == newHunterUserId)
            throw new ArgumentException("This player is already the hunter.", nameof(newHunterUserId));

        var newHunter = _participants.FirstOrDefault(p => p.UserId == newHunterUserId && p.Role == ParticipantRole.Prey)
            ?? throw new ArgumentException("The new hunter must be an existing prey of the game.", nameof(newHunterUserId));

        currentHunter.ChangeRole(ParticipantRole.Prey);
        newHunter.ChangeRole(ParticipantRole.Hunter);
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
        foreach (var participant in _participants.Where(p => p.Role == ParticipantRole.Prey))
        {
            if (participant.TryTransitionByTimeout(now, out var newState))
                changes.Add((participant.UserId, newState));
        }
        return changes;
    }

    /// <summary>
    /// Marks a prey participant as Tagged. The caller must be the hunter; the target must exist
    /// as a prey in Active or Passive state; the game must be InProgress.
    /// </summary>
    public void TagParticipant(Guid callerId, Guid targetUserId)
    {
        if (Status != GameStatus.InProgress)
            throw new InvalidOperationException("Players can only be tagged while the game is in progress.");

        if (Hunter?.UserId != callerId)
            throw new UnauthorizedAccessException("Only the hunter can tag preys.");

        var target = _participants.FirstOrDefault(p => p.UserId == targetUserId && p.Role == ParticipantRole.Prey)
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
        var preys = _participants.Where(p => p.Role == ParticipantRole.Prey).ToList();
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

        if (participant.Role != ParticipantRole.Prey)
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

    private static void ValidateGameCode(string gameCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameCode);

        if (gameCode.Length != GameCodeLength || !gameCode.All(char.IsAsciiDigit))
            throw new ArgumentException(
                $"A game code must consist of exactly {GameCodeLength} decimal digits.", nameof(gameCode));
    }
}
