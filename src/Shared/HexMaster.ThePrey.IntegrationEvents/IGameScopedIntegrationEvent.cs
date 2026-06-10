namespace HexMaster.ThePrey.IntegrationEvents;

/// <summary>An integration event that belongs to a single game, so it can be routed to that game's group.</summary>
public interface IGameScopedIntegrationEvent : IIntegrationEvent
{
    Guid GameId { get; }
}
