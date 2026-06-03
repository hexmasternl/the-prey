namespace HexMaster.ThePrey.Games.DomainModels;

/// <summary>
/// The tunable rules of a game. Durations expressed in minutes; location intervals in seconds.
/// Use <see cref="Create"/> to obtain an instance with all interdependent invariants enforced.
/// </summary>
public sealed record GameConfiguration
{
    /// <summary>Total length of the game, in minutes.</summary>
    public int GameDuration { get; private init; }

    /// <summary>Head start, in minutes, after the game start before hunters may move.</summary>
    public int HunterDelayTime { get; private init; }

    /// <summary>Length, in minutes, of the final stage (the last minutes of the game). Must be smaller than <see cref="GameDuration"/>.</summary>
    public int FinalStageDuration { get; private init; }

    /// <summary>Default interval, in seconds, at which preys report their GPS location.</summary>
    public int DefaultLocationInterval { get; private init; }

    /// <summary>Interval, in seconds, at which locations are reported during the final stage.</summary>
    public int FinalLocationInterval { get; private init; }

    /// <summary>Enables a penalty for preys that leave the play-field boundary.</summary>
    public bool EnablePreyBoundaryPenalties { get; private init; }

    /// <summary>Enables a penalty for hunters that move before <see cref="HunterDelayTime"/> has elapsed.</summary>
    public bool EnableHunterBoundaryPenalty { get; private init; }

    private GameConfiguration() { }

    public static GameConfiguration Create(
        int gameDuration,
        int hunterDelayTime,
        int finalStageDuration,
        int defaultLocationInterval,
        int finalLocationInterval,
        bool enablePreyBoundaryPenalties = false,
        bool enableHunterBoundaryPenalty = false)
    {
        if (gameDuration <= 0)
            throw new ArgumentOutOfRangeException(nameof(gameDuration), gameDuration, "Game duration must be greater than zero minutes.");

        if (defaultLocationInterval <= 0)
            throw new ArgumentOutOfRangeException(nameof(defaultLocationInterval), defaultLocationInterval, "Default location interval must be greater than zero seconds.");

        if (finalLocationInterval <= 0)
            throw new ArgumentOutOfRangeException(nameof(finalLocationInterval), finalLocationInterval, "Final location interval must be greater than zero seconds.");

        if (hunterDelayTime < 0 || hunterDelayTime >= gameDuration)
            throw new ArgumentOutOfRangeException(nameof(hunterDelayTime), hunterDelayTime, "Hunter delay time must be zero or greater and smaller than the game duration.");

        if (finalStageDuration <= 0 || finalStageDuration >= gameDuration)
            throw new ArgumentOutOfRangeException(nameof(finalStageDuration), finalStageDuration, "Final stage duration must be greater than zero and smaller than the game duration.");

        if (finalLocationInterval > defaultLocationInterval)
            throw new ArgumentException("Final location interval must be less than or equal to the default location interval.", nameof(finalLocationInterval));

        return new GameConfiguration
        {
            GameDuration = gameDuration,
            HunterDelayTime = hunterDelayTime,
            FinalStageDuration = finalStageDuration,
            DefaultLocationInterval = defaultLocationInterval,
            FinalLocationInterval = finalLocationInterval,
            EnablePreyBoundaryPenalties = enablePreyBoundaryPenalties,
            EnableHunterBoundaryPenalty = enableHunterBoundaryPenalty
        };
    }
}
