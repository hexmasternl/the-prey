namespace HexMaster.ThePrey.Games.Abstractions.DataTransferObjects;

/// <summary>
/// Game tuning values. Durations (<see cref="GameDuration"/>, <see cref="HunterDelayTime"/>,
/// <see cref="FinalStageDuration"/>) are in minutes; intervals are in seconds.
/// </summary>
public sealed record GameConfigurationDto(
    int GameDuration,
    int HunterDelayTime,
    int FinalStageDuration,
    int DefaultLocationInterval,
    int FinalLocationInterval,
    bool EnablePreyBoundaryPenalties,
    bool EnableHunterBoundaryPenalty);
