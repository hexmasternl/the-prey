using HexMaster.ThePrey.Games.Abstractions.DataTransferObjects;

namespace HexMaster.ThePrey.Games.Features.SetHunter;

public sealed record SetHunterCommand(Guid GameId, Guid CallerUserId, Guid NewHunterUserId);

public sealed record SetHunterResult(GameDto Game);
