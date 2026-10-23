namespace HexMaster.ThePrey.Games.DomainModels;

/// <summary>
/// A penalty applied to a participant. <see cref="EndsAt"/> marks the moment the penalty expires;
/// while a penalty is active the participant must report its location more frequently.
/// </summary>
public sealed record Penalty(Guid Id, DateTimeOffset EndsAt)
{
    public static Penalty Create(DateTimeOffset endsAt) => new(Guid.NewGuid(), endsAt);

    /// <summary>True when the penalty has not yet expired at the supplied moment.</summary>
    public bool IsActive(DateTimeOffset now) => EndsAt > now;
}
