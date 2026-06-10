namespace HexMaster.ThePrey.Games.DomainModels;

/// <summary>
/// A player who has joined a game. Before it starts they are in the lobby (IsReady flag);
/// once it starts they become an active participant. Role (hunter vs prey) is derived from
/// <see cref="Game.HunterUserId"/> — not stored here.
/// A child entity of the <see cref="Game"/> aggregate — created and mutated only through the aggregate root.
/// </summary>
public sealed class GameParticipant
{
    private readonly List<Penalty> _penalties = [];
    private readonly List<LocationReading> _locations = [];

    public Guid UserId { get; private set; }
    public string DisplayName { get; private set; } = default!;
    public string? ProfilePictureUrl { get; private set; }
    public bool IsReady { get; private set; }
    public PlayerState State { get; private set; } = PlayerState.Active;
    public DateTimeOffset? LastLocationAt { get; private set; }
    public GpsCoordinate? Location { get; private set; }
    public IReadOnlyList<Penalty> Penalties => _penalties.AsReadOnly();
    public IReadOnlyList<LocationReading> Locations => _locations.AsReadOnly();

    private GameParticipant() { }

    /// <summary>Creates a new participant from display info. State=Active, IsReady=false.</summary>
    public static GameParticipant Create(Guid userId, string displayName, string? profilePictureUrl = null)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("A participant requires a non-empty user identifier.", nameof(userId));

        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        return new GameParticipant
        {
            UserId = userId,
            DisplayName = displayName,
            ProfilePictureUrl = string.IsNullOrWhiteSpace(profilePictureUrl) ? null : profilePictureUrl,
            IsReady = false,
            State = PlayerState.Active
        };
    }

    /// <summary>Reconstructs a previously-persisted participant. Intended only for data adapters.</summary>
    public static GameParticipant Rehydrate(
        Guid userId,
        string displayName,
        string? profilePictureUrl,
        bool isReady,
        GpsCoordinate? location,
        IEnumerable<Penalty> penalties,
        IEnumerable<LocationReading> locations,
        PlayerState state = PlayerState.Active,
        DateTimeOffset? lastLocationAt = null)
    {
        var participant = new GameParticipant
        {
            UserId = userId,
            DisplayName = displayName,
            ProfilePictureUrl = profilePictureUrl,
            IsReady = isReady,
            Location = location,
            State = state,
            LastLocationAt = lastLocationAt
        };
        participant._penalties.AddRange(penalties);
        participant._locations.AddRange(locations);
        return participant;
    }

    /// <summary>Sets the ready flag. Called by the aggregate.</summary>
    internal void SetReady(bool isReady) => IsReady = isReady;

    /// <summary>
    /// Activates the participant and records the location timestamp. No-op when already Out or Tagged.
    /// Returns the state before the call.
    /// </summary>
    internal PlayerState ActivateIfAllowed(DateTimeOffset at)
    {
        var previous = State;
        if (State != PlayerState.Out && State != PlayerState.Tagged)
        {
            State = PlayerState.Active;
            LastLocationAt = at;
        }
        return previous;
    }

    /// <summary>
    /// Applies timeout-based transitions. Returns true (and sets newState) when the state changed.
    /// Out and Tagged participants are never transitioned.
    /// </summary>
    internal bool TryTransitionByTimeout(DateTimeOffset now, out PlayerState newState)
    {
        newState = State;
        if (State == PlayerState.Out || State == PlayerState.Tagged || LastLocationAt is null)
            return false;

        var silentMinutes = (now - LastLocationAt.Value).TotalMinutes;

        if (silentMinutes >= 7)
        {
            newState = PlayerState.Out;
            State = PlayerState.Out;
            return true;
        }

        if (silentMinutes >= 5 && State == PlayerState.Active)
        {
            newState = PlayerState.Passive;
            State = PlayerState.Passive;
            return true;
        }

        return false;
    }

    /// <summary>Marks the participant as Tagged. Throws when the participant is not Active or Passive.</summary>
    internal void Tag()
    {
        if (State != PlayerState.Active && State != PlayerState.Passive)
            throw new InvalidOperationException("Only an Active or Passive prey can be tagged.");
        State = PlayerState.Tagged;
    }

    /// <summary>Marks the participant as Out (forfeit). Throws when already Out or Tagged.</summary>
    internal void ForfeitOut()
    {
        if (State == PlayerState.Out || State == PlayerState.Tagged)
            throw new InvalidOperationException("A participant that is already Out or Tagged cannot forfeit.");
        State = PlayerState.Out;
    }

    internal void RecordLocation(LocationReading reading)
    {
        ArgumentNullException.ThrowIfNull(reading);
        _locations.Add(reading);
    }

    /// <summary>
    /// Updates the broadcasted location. Called exclusively by the game engine broadcast cycle
    /// after selecting the participant's most recent location from history.
    /// </summary>
    public void UpdateBroadcastLocation(GpsCoordinate coordinate)
    {
        ArgumentNullException.ThrowIfNull(coordinate);
        Location = coordinate;
    }

    internal void ApplyPenalty(Penalty penalty)
    {
        ArgumentNullException.ThrowIfNull(penalty);
        _penalties.Add(penalty);
    }

    /// <summary>True when the participant has at least one penalty that has not yet expired.</summary>
    public bool HasActivePenalty(DateTimeOffset now) => _penalties.Any(p => p.IsActive(now));

    /// <summary>
    /// The expiry of the participant's active penalty, or null when none is active. When multiple
    /// penalties overlap, the latest expiry wins.
    /// </summary>
    public DateTimeOffset? ActivePenaltyEndsAt(DateTimeOffset now) =>
        _penalties.Where(p => p.IsActive(now)).Select(p => (DateTimeOffset?)p.EndsAt).Max();
}
