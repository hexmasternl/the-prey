package nl.hexmaster.theprey.ui.games.create

/** The five game settings a player chooses, each with its options and default, all in minutes. */
enum class GameSetting(val options: List<Int>, val default: Int) {
    Duration(listOf(30, 60, 90), 30),
    HunterDelay(listOf(3, 5, 10), 5),
    FinalStage(listOf(3, 5, 10), 10),
    PingInterval(listOf(2, 3, 5), 3),
    FinalPingInterval(listOf(1, 2, 3), 1),
}

/**
 * A complete, always server-valid set of game settings in minutes. With the current options the
 * hunter delay and the final stage are always shorter than the shortest duration, so the one
 * cross-rule is that the final-stage ping interval never exceeds the normal ping interval.
 */
data class GameSettings(
    val duration: Int = GameSetting.Duration.default,
    val hunterDelay: Int = GameSetting.HunterDelay.default,
    val finalStage: Int = GameSetting.FinalStage.default,
    val pingInterval: Int = GameSetting.PingInterval.default,
    val finalPingInterval: Int = GameSetting.FinalPingInterval.default,
) {
    fun valueOf(setting: GameSetting): Int = when (setting) {
        GameSetting.Duration -> duration
        GameSetting.HunterDelay -> hunterDelay
        GameSetting.FinalStage -> finalStage
        GameSetting.PingInterval -> pingInterval
        GameSetting.FinalPingInterval -> finalPingInterval
    }

    /** Whether [minutes] may be chosen for [setting] given the other choices. */
    fun isEnabled(setting: GameSetting, minutes: Int): Boolean = when (setting) {
        GameSetting.FinalPingInterval -> minutes <= pingInterval
        else -> true
    } && minutes in setting.options

    /**
     * These settings with [setting] set to [minutes]; values that are not an option, or not
     * enabled, are ignored. Lowering the ping interval moves the final-stage ping interval down to
     * the largest option that is still allowed.
     */
    fun with(setting: GameSetting, minutes: Int): GameSettings {
        if (!isEnabled(setting, minutes)) return this
        return when (setting) {
            GameSetting.Duration -> copy(duration = minutes)
            GameSetting.HunterDelay -> copy(hunterDelay = minutes)
            GameSetting.FinalStage -> copy(finalStage = minutes)
            GameSetting.PingInterval -> copy(
                pingInterval = minutes,
                finalPingInterval = if (finalPingInterval <= minutes) {
                    finalPingInterval
                } else {
                    GameSetting.FinalPingInterval.options.filter { it <= minutes }.max()
                },
            )
            GameSetting.FinalPingInterval -> copy(finalPingInterval = minutes)
        }
    }
}
