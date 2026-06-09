namespace HexMaster.ThePrey.Games.Notifications;

public abstract record GameEvent(Guid GameId, string EventType);

public sealed record StateChangedEvent(Guid GameId, string NewState)
    : GameEvent(GameId, "state-changed");

public sealed record ParticipantLocatedEvent(Guid GameId, Guid UserId, string ParticipantRole, double Latitude, double Longitude, string ParticipantState)
    : GameEvent(GameId, "participant-located");

public sealed record ParticipantStatusChangedEvent(Guid GameId, Guid ParticipantId, string ParticipantRole, string NewState)
    : GameEvent(GameId, "participant-status-changed");

public sealed record GameEndedEvent(Guid GameId, string Outcome, int SurvivorCount)
    : GameEvent(GameId, "game-ended");
