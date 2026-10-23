using HexMaster.ThePrey.Games.Abstractions.DataTransferObjects;

namespace HexMaster.ThePrey.Games.Features.RemoveLobbyPlayer;

public sealed record RemoveLobbyPlayerCommand(Guid GameId, Guid OwnerUserId, Guid TargetUserId);

public sealed record RemoveLobbyPlayerResult(GameDto Game);
