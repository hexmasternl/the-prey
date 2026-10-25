package nl.hexmaster.theprey.ui.games.create

import nl.hexmaster.theprey.data.game.GameConfigurationDto

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

private const val SECONDS_PER_MINUTE = 60

/**
 * The server's configuration as settings in minutes. Values the server holds that are not an
 * option (e.g. a game made by another client) are kept as they are: they show with no segment
 * selected.
 */
fun GameConfigurationDto.toSettings(): GameSettings = GameSettings(
    duration = gameDuration,
    hunterDelay = hunterDelayTime,
    finalStage = finalStageDuration,
    pingInterval = defaultLocationInterval / SECONDS_PER_MINUTE,
    finalPingInterval = finalLocationInterval / SECONDS_PER_MINUTE,
)

/** [settings] applied to this configuration: ping intervals back to seconds, penalty flags unchanged. */
fun GameConfigurationDto.with(settings: GameSettings): GameConfigurationDto = copy(
    gameDuration = settings.duration,
    hunterDelayTime = settings.hunterDelay,
    finalStageDuration = settings.finalStage,
    defaultLocationInterval = settings.pingInterval * SECONDS_PER_MINUTE,
    finalLocationInterval = settings.finalPingInterval * SECONDS_PER_MINUTE,
)
