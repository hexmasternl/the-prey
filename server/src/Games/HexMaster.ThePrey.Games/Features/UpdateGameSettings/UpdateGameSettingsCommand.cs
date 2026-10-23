using HexMaster.ThePrey.Games.Abstractions.DataTransferObjects;

namespace HexMaster.ThePrey.Games.Features.UpdateGameSettings;

public sealed record UpdateGameSettingsCommand(
    Guid GameId,
    Guid OwnerUserId,
    int GameDuration,
    int HunterDelayTime,
    int FinalStageDuration,
    int DefaultLocationInterval,
    int FinalLocationInterval,
    bool EnablePreyBoundaryPenalties,
    bool EnableHunterBoundaryPenalty);

public sealed record UpdateGameSettingsResult(GameDto Game);
