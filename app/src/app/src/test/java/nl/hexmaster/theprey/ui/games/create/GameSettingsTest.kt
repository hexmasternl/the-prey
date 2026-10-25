package nl.hexmaster.theprey.ui.games.create

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class GameSettingsTest {

    @Test
    fun defaults_ShouldBe30_5_10_3_1_WhenCreated() {
        assertEquals(GameSettings(30, 5, 10, 3, 1), GameSettings())
    }

    @Test
    fun with_ShouldChangeOnlyThatSetting_WhenValueIsAnOption() {
        assertEquals(GameSettings(duration = 60), GameSettings().with(GameSetting.Duration, 60))
    }

    @Test
    fun with_ShouldIgnoreValue_WhenItIsNotAnOption() {
        val settings = GameSettings()

        assertEquals(settings, settings.with(GameSetting.Duration, 45))
        assertEquals(settings, settings.with(GameSetting.PingInterval, 7))
    }

    @Test
    fun isEnabled_ShouldDisableFinalPing3_WhenPingIntervalIs2() {
        val settings = GameSettings(pingInterval = 2)

        assertTrue(settings.isEnabled(GameSetting.FinalPingInterval, 1))
        assertTrue(settings.isEnabled(GameSetting.FinalPingInterval, 2))
        assertFalse(settings.isEnabled(GameSetting.FinalPingInterval, 3))
    }

    @Test
    fun isEnabled_ShouldEnableEveryFinalPing_WhenPingIntervalIs3Or5() {
        listOf(3, 5).forEach { ping ->
            val settings = GameSettings(pingInterval = ping)
            GameSetting.FinalPingInterval.options.forEach { assertTrue(settings.isEnabled(GameSetting.FinalPingInterval, it)) }
        }
    }

    @Test
    fun with_ShouldClampFinalPing3To2_WhenPingIntervalIsLoweredTo2() {
        val settings = GameSettings(pingInterval = 3, finalPingInterval = 3)

        assertEquals(GameSettings(pingInterval = 2, finalPingInterval = 2), settings.with(GameSetting.PingInterval, 2))
    }

    @Test
    fun with_ShouldKeepFinalPing1_WhenPingIntervalIsLoweredTo2() {
        val settings = GameSettings(pingInterval = 3, finalPingInterval = 1)

        assertEquals(1, settings.with(GameSetting.PingInterval, 2).finalPingInterval)
    }

    @Test
    fun with_ShouldIgnoreDisabledFinalPing_WhenItExceedsPingInterval() {
        val settings = GameSettings(pingInterval = 2)

        assertEquals(settings, settings.with(GameSetting.FinalPingInterval, 3))
    }

    /** Mirrors the server's `GameConfiguration.Create` rules (durations in minutes, intervals in seconds). */
    private fun isServerValid(s: GameSettings): Boolean {
        val defaultInterval = s.pingInterval * 60
        val finalInterval = s.finalPingInterval * 60
        return s.duration > 0 &&
            defaultInterval > 0 &&
            finalInterval > 0 &&
            s.hunterDelay >= 0 && s.hunterDelay < s.duration &&
            s.finalStage > 0 && s.finalStage < s.duration &&
            finalInterval <= defaultInterval
    }

    @Test
    fun everyEnabledCombination_ShouldSatisfyServerRules_WhenBuiltThroughWith() {
        var checked = 0
        for (duration in GameSetting.Duration.options)
            for (delay in GameSetting.HunterDelay.options)
                for (stage in GameSetting.FinalStage.options)
                    for (ping in GameSetting.PingInterval.options) {
                        val base = GameSettings()
                            .with(GameSetting.Duration, duration)
                            .with(GameSetting.HunterDelay, delay)
                            .with(GameSetting.FinalStage, stage)
                            .with(GameSetting.PingInterval, ping)
                        for (finalPing in GameSetting.FinalPingInterval.options) {
                            if (!base.isEnabled(GameSetting.FinalPingInterval, finalPing)) continue
                            val settings = base.with(GameSetting.FinalPingInterval, finalPing)
                            assertTrue("Invalid for the server: $settings", isServerValid(settings))
                            checked++
                        }
                    }
        assertTrue(checked > 0)
    }
}
