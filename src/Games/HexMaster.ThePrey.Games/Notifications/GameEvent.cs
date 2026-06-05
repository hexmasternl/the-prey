namespace HexMaster.ThePrey.Games.Notifications;

public abstract record GameEvent(Guid GameId, string EventType);

public sealed record StateChangedEvent(Guid GameId, string NewState)
    : GameEvent(GameId, "state-changed");

public sealed record ParticipantLocatedEvent(Guid GameId, Guid UserId, string ParticipantRole, double Latitude, double Longitude)
    : GameEvent(GameId, "participant-located");

public sealed record GameEndedEvent(Guid GameId)
    : GameEvent(GameId, "game-ended");
