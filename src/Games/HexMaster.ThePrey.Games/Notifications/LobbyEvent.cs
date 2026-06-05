using HexMaster.ThePrey.Games.Abstractions.DataTransferObjects;

namespace HexMaster.ThePrey.Games.Notifications;

public sealed record LobbyEvent(Guid GameId, string EventType, GameDto Payload);
