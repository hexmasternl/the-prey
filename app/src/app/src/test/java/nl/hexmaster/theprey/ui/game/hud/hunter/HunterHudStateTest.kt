package nl.hexmaster.theprey.ui.game.hud.hunter

import nl.hexmaster.theprey.data.game.FullGameStateDto
import nl.hexmaster.theprey.data.game.GameConfigurationDto
import nl.hexmaster.theprey.data.game.GameDto
import nl.hexmaster.theprey.data.game.GameStatusDto
import nl.hexmaster.theprey.data.game.GameStatuses
import nl.hexmaster.theprey.data.game.ParticipantDto
import nl.hexmaster.theprey.data.game.PlayerStates
import nl.hexmaster.theprey.data.location.GeoPosition
import nl.hexmaster.theprey.data.playfield.GpsCoordinateDto
import nl.hexmaster.theprey.ui.game.ActiveGameState
import nl.hexmaster.theprey.ui.game.ActiveGameUi
import nl.hexmaster.theprey.ui.game.StartDelay
import nl.hexmaster.theprey.ui.game.hud.common.ServerClock
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test
import java.time.Instant

class HunterHudStateTest {

    private val now = Instant.parse("2026-08-31T11:40:00Z")
    private val own = GeoPosition(52.0, 5.0)

    // ---- clock band ----

    @Test
    fun of_ShouldMapBands_WhenAtTheBoundaries() {
        assertEquals(ClockBand.Normal, ClockBand.of(601, false))
        assertEquals(ClockBand.Low, ClockBand.of(600, false))
        assertEquals(ClockBand.Low, ClockBand.of(301, false))
        assertEquals(ClockBand.Critical, ClockBand.of(300, false))
        assertEquals(ClockBand.Critical, ClockBand.of(0, false))
    }

    @Test
    fun of_ShouldBePenalty_WhenPenaltyActiveAtAnyTime() {
        listOf(1_800, 601, 600, 300, 0).forEach { assertEquals(ClockBand.Penalty, ClockBand.of(it, true)) }
    }

    // ---- derivation ----

    private fun prey(id: String, state: String = PlayerStates.ACTIVE, northMeters: Double? = null, penalty: Boolean = false, endsAt: String? = null) =
        ParticipantDto(
            userId = id,
            state = state,
            lastKnownLocation = northMeters?.let { GpsCoordinateDto(52.0 + it / 111_195.0, 5.0) },
            hasActivePenalty = penalty,
            penaltyEndsAt = endsAt,
        )

    private fun ready(
        preys: List<ParticipantDto>,
        hunterPenalty: Boolean = false,
        hunterPenaltyEndsAt: String? = null,
        own: GeoPosition? = this.own,
        hunterMayMoveAt: String? = null,
        lastPing: Long? = null,
    ): ActiveGameUi.Ready {
        val snapshot = FullGameStateDto(
            game = GameDto(
                id = "g",
                status = GameStatuses.IN_PROGRESS,
                configuration = GameConfigurationDto(60),
                participants = listOf(ParticipantDto("me", PlayerStates.ACTIVE, hasActivePenalty = hunterPenalty, penaltyEndsAt = hunterPenaltyEndsAt)) + preys,
                hunterUserId = "me",
            ),
            serverTime = "2026-08-31T11:40:00Z",
            status = GameStatusDto(gameDurationLeft = 754, nextPingDuration = 18, currentPingInterval = 180, hunterMayMoveAt = hunterMayMoveAt),
        )
        return ActiveGameUi.Ready(
            game = ActiveGameState.from(snapshot, "me"),
            snapshot = snapshot,
            own = own,
            heading = 0f,
            clock = ServerClock(now, seededAtElapsed = 1_000),
            delay = StartDelay.seed(hunterMayMoveAt, snapshot.serverTime, 1_000),
            lastPingAtElapsed = lastPing,
        )
    }

    private val sixPreys = listOf(
        prey("a"), prey("b"), prey("c", PlayerStates.PASSIVE), prey("d"),
        prey("e", PlayerStates.TAGGED), prey("f", PlayerStates.OUT),
    )

    @Test
    fun from_ShouldCountFourOfSix_WhenOneTaggedAndOneOut() {
        val state = HunterHudState.from(ready(sixPreys), now)

        assertEquals(4, state.preysInPlay)
        assertEquals(6, state.preysTotal)
    }

    @Test
    fun from_ShouldDropToThreeOfSix_WhenAnotherPreyIsTagged() {
        val afterTag = sixPreys.map { if (it.userId == "a") it.copy(state = PlayerStates.TAGGED) else it }

        assertEquals(3, HunterHudState.from(ready(afterTag), now).preysInPlay)
    }

    @Test
    fun from_ShouldUseNearestInPlayPrey_WhenSeveralLocationsKnown() {
        val preys = listOf(prey("a", northMeters = 320.0), prey("b", northMeters = 85.0))

        assertEquals(85, HunterHudState.from(ready(preys), now).closestPreyMeters)
    }

    @Test
    fun from_ShouldIgnoreTaggedPrey_WhenItIsNearest() {
        val preys = listOf(prey("t", PlayerStates.TAGGED, northMeters = 20.0), prey("a", northMeters = 140.0))

        assertEquals(140, HunterHudState.from(ready(preys), now).closestPreyMeters)
    }

    @Test
    fun from_ShouldHaveNoDistance_WhenNoOwnFixOrNoPreyLocations() {
        assertNull(HunterHudState.from(ready(listOf(prey("a", northMeters = 50.0)), own = null), now).closestPreyMeters)
        assertNull(HunterHudState.from(ready(listOf(prey("a"))), now).closestPreyMeters)
    }

    @Test
    fun from_ShouldCountPenalisedPreys_WhenPenaltiesActiveOrExpired() {
        val preys = listOf(
            prey("a", penalty = true),
            prey("b", penalty = true),
            prey("c", penalty = true, endsAt = "2026-08-31T11:39:00Z"),
            prey("d"),
        )

        assertEquals(2, HunterHudState.from(ready(preys), now).penalisedPreys)
    }

    @Test
    fun from_ShouldReportHunterPenalty_WhenOwnPenaltyIsActive() {
        val state = HunterHudState.from(ready(listOf(prey("a")), hunterPenalty = true), now)

        assertTrue(state.hunterPenaltyActive)
        assertEquals(ClockBand.Penalty, state.clockBand(1_000))
    }

    @Test
    fun from_ShouldHonourSnapshotPenaltyEnds_WhenEndTimesAreKnown() {
        val preys = listOf(
            prey("a", penalty = true, endsAt = "2026-08-31T11:44:00Z"),
            prey("b", penalty = true, endsAt = "2026-08-31T11:40:00Z"),
        )

        val running = HunterHudState.from(ready(preys, hunterPenalty = true, hunterPenaltyEndsAt = "2026-08-31T11:44:00Z"), now)
        val expired = HunterHudState.from(ready(preys, hunterPenalty = true, hunterPenaltyEndsAt = "2026-08-31T11:39:00Z"), now)

        assertEquals(1, running.penalisedPreys)
        assertTrue(running.hunterPenaltyActive)
        assertFalse(expired.hunterPenaltyActive)
    }

    // ---- countdowns ----

    @Test
    fun tagUnlock_ShouldStayLockedUntilServerMoment_WhenDelayRuns() {
        val state = HunterHudState.from(ready(listOf(prey("a")), hunterMayMoveAt = "2026-08-31T11:42:00Z"), now)

        assertFalse(state.tagUnlock.isUnlocked(1_000))
        assertFalse(state.tagUnlock.isUnlocked(120_999))
        assertTrue(state.tagUnlock.isUnlocked(121_000))
        assertTrue(state.tagUnlock.isUnlocked(500_000))
    }

    @Test
    fun tagUnlock_ShouldBeUnlocked_WhenMoveMomentHasPassed() {
        val state = HunterHudState.from(ready(listOf(prey("a")), hunterMayMoveAt = "2026-08-31T11:30:00Z"), now)

        assertTrue(state.tagUnlock.isUnlocked(1_000))
    }

    @Test
    fun tagUnlock_ShouldIgnoreDeviceClock_WhenDeviceIsSkewed() {
        // Seeded from hunterMayMoveAt - serverTime only: the device wall clock never enters.
        val state = HunterHudState.from(ready(listOf(prey("a")), hunterMayMoveAt = "2026-08-31T11:41:00Z"), now)

        assertEquals(60_000L, state.tagUnlock.remainingMillis(1_000))
    }

    @Test
    fun nextPing_ShouldSeedFromSnapshot_WhenNoPingYet() {
        val state = HunterHudState.from(ready(listOf(prey("a"))), now)

        assertEquals(18, state.nextPing.secondsLeft(1_000))
        assertEquals(17, state.nextPing.secondsLeft(2_000))
    }

    @Test
    fun nextPing_ShouldReseedFromInterval_WhenALocationsBatchArrived() {
        val state = HunterHudState.from(ready(listOf(prey("a")), lastPing = 30_000), now)

        assertEquals(180, state.nextPing.secondsLeft(30_000))
        assertEquals(179, state.nextPing.secondsLeft(31_000))
    }
}
