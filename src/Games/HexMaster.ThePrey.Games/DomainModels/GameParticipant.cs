namespace HexMaster.ThePrey.Games.DomainModels;

/// <summary>
/// A player taking part in a running game, in the role of either the hunter or a prey. Carries the
/// player's current location, the penalties applied to them, and their reported location history.
/// A child entity of the <see cref="Game"/> aggregate — created and mutated only through the aggregate root.
/// </summary>
public sealed class GameParticipant
{
    private readonly List<Penalty> _penalties = [];
    private readonly List<LocationReading> _locations = [];

    public Guid UserId { get; private set; }
    public ParticipantRole Role { get; private set; }
    public GpsCoordinate? Location { get; private set; }
    public IReadOnlyList<Penalty> Penalties => _penalties.AsReadOnly();
    public IReadOnlyList<LocationReading> Locations => _locations.AsReadOnly();

    private GameParticipant() { }

    internal static GameParticipant Create(Guid userId, ParticipantRole role)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("A participant requires a non-empty user identifier.", nameof(userId));

        return new GameParticipant
        {
            UserId = userId,
            Role = role
        };
    }

    /// <summary>Reconstructs a previously-persisted participant. Intended only for data adapters.</summary>
    public static GameParticipant Rehydrate(
        Guid userId,
        ParticipantRole role,
        GpsCoordinate? location,
        IEnumerable<Penalty> penalties,
        IEnumerable<LocationReading> locations)
    {
        var participant = new GameParticipant
        {
            UserId = userId,
            Role = role,
            Location = location
        };
        participant._penalties.AddRange(penalties);
        participant._locations.AddRange(locations);
        return participant;
    }

    internal void ChangeRole(ParticipantRole role) => Role = role;

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
