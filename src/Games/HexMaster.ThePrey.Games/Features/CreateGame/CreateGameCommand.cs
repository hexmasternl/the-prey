using HexMaster.ThePrey.Games.Abstractions.DataTransferObjects;

namespace HexMaster.ThePrey.Games.Features.CreateGame;

public sealed record CreateGameCommand(
    Guid OwnerUserId,
    Guid PlayfieldId,
    string DisplayName,
    string? ProfilePictureUrl,
    int GameDuration,
    int HunterDelayTime,
    int FinalStageDuration,
    int DefaultLocationInterval,
    int FinalLocationInterval,
    bool EnablePreyBoundaryPenalties,
    bool EnableHunterBoundaryPenalty);

public sealed record CreateGameResult(GameDto Game);
