package nl.hexmaster.theprey.ui.game.hud.prey

import nl.hexmaster.theprey.data.game.FullGameStateDto
import nl.hexmaster.theprey.data.game.GameConfigurationDto
import nl.hexmaster.theprey.data.game.GameDto
import nl.hexmaster.theprey.data.game.GameStatusDto
import nl.hexmaster.theprey.data.game.GameStatuses
import nl.hexmaster.theprey.data.game.ParticipantDto
import nl.hexmaster.theprey.data.game.PlayerStates
import nl.hexmaster.theprey.data.game.RoleStateDto
import nl.hexmaster.theprey.data.location.GeoPosition
import nl.hexmaster.theprey.data.playfield.GpsCoordinateDto
import nl.hexmaster.theprey.ui.game.ActiveGameState
import nl.hexmaster.theprey.ui.game.ActiveGameUi
import nl.hexmaster.theprey.ui.game.hud.common.ServerClock
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test
import java.time.Instant

class PreyHudStateTest {

    private val now = Instant.parse("2026-08-31T11:40:00Z")

    // ---- bands ----

    @Test
    fun of_ShouldMapBands_WhenAtTheBoundaries() {
        assertEquals(ProximityBand.Unknown, ProximityBand.of(null))
        assertEquals(ProximityBand.Safe, ProximityBand.of(250))
        assertEquals(ProximityBand.Close, ProximityBand.of(249))
        assertEquals(ProximityBand.Close, ProximityBand.of(100))
        assertEquals(ProximityBand.Warning, ProximityBand.of(99))
        assertEquals(ProximityBand.Warning, ProximityBand.of(0))
    }

    // ---- penalty label ----

    @Test
    fun penaltyLabelAt_ShouldAlternateEverySecondStartingWithPenalty() {
        assertEquals(RoleLabel.Penalty, penaltyLabelAt(0))
        assertEquals(RoleLabel.Penalty, penaltyLabelAt(999))
        assertEquals(RoleLabel.Prey, penaltyLabelAt(1_000))
        assertEquals(RoleLabel.Penalty, penaltyLabelAt(2_000))
    }

    // ---- closest hunter ----

    @Test
    fun closestHunterMeters_ShouldUseTheNearest_WhenTwoHunters() {
        val own = GeoPosition(52.0, 5.0)
        // 0.0027° and 0.00108° north: about 300 m and 120 m.
        val far = GeoPosition(52.0027, 5.0)
        val near = GeoPosition(52.00108, 5.0)

        assertEquals(120, closestHunterMeters(own, listOf(far, near)))
        assertEquals(ProximityBand.Close, ProximityBand.of(closestHunterMeters(own, listOf(far, near))))
    }

    @Test
    fun closestHunterMeters_ShouldBeNull_WhenNoHuntersOrNoOwnFix() {
        assertNull(closestHunterMeters(GeoPosition(52.0, 5.0), emptyList()))
        assertNull(closestHunterMeters(null, listOf(GeoPosition(52.0, 5.0))))
    }

    // ---- derivation ----

    private fun ready(
        hunterLocation: GpsCoordinateDto? = null,
        own: GeoPosition? = null,
        serverDistance: Int? = 300,
        myPenalty: Boolean = false,
        myPenaltyEndsAt: String? = null,
    ): ActiveGameUi.Ready {
        val snapshot = FullGameStateDto(
            game = GameDto(
                id = "g",
                status = GameStatuses.IN_PROGRESS,
                configuration = GameConfigurationDto(60),
                participants = listOf(
                    ParticipantDto("me", PlayerStates.ACTIVE, hasActivePenalty = myPenalty, penaltyEndsAt = myPenaltyEndsAt),
                    ParticipantDto("h", PlayerStates.ACTIVE, lastKnownLocation = hunterLocation),
                ),
                hunterUserId = "h",
            ),
            serverTime = "2026-08-31T11:40:00Z",
            status = GameStatusDto(gameDurationLeft = 754, nextPingDuration = 18, currentPingInterval = 30),
            roleState = RoleStateDto(hunterDistanceMeters = serverDistance),
        )
        return ActiveGameUi.Ready(
            game = ActiveGameState.from(snapshot, "me"),
            snapshot = snapshot,
            own = own,
            heading = 0f,
            clock = ServerClock(now, seededAtElapsed = 7_000),
            delay = null,
        )
    }

    @Test
    fun from_ShouldUseServerDistanceAndSeedCountdowns_WhenOnlyTheSnapshotIsKnown() {
        val state = PreyHudState.from(ready(hunterLocation = GpsCoordinateDto(52.0, 5.0), own = null), now)

        assertEquals(300, state.hunterDistanceMeters)
        assertEquals(ProximityBand.Safe, state.band)
        assertEquals(754, state.gameTime.secondsLeft(7_000))
        assertEquals(18, state.nextBroadcast.secondsLeft(7_000))
        assertEquals(17, state.nextBroadcast.secondsLeft(8_000))
        assertEquals(30, state.broadcastIntervalSeconds)
    }

    @Test
    fun from_ShouldRecomputeLocally_WhenHunterLocationAndOwnFixAreKnown() {
        // A live update put the hunter about 80 m north.
        val state = PreyHudState.from(
            ready(hunterLocation = GpsCoordinateDto(52.00072, 5.0), own = GeoPosition(52.0, 5.0), serverDistance = 300),
            now,
        )

        assertEquals(80, state.hunterDistanceMeters)
        assertEquals(ProximityBand.Warning, state.band)
    }

    @Test
    fun from_ShouldBeUnknown_WhenNoHunterLocationAndNoServerDistance() {
        val state = PreyHudState.from(ready(hunterLocation = null, own = GeoPosition(52.0, 5.0), serverDistance = null), now)

        assertNull(state.hunterDistanceMeters)
        assertEquals(ProximityBand.Unknown, state.band)
    }

    @Test
    fun from_ShouldReportPenalty_WhenOwnPenaltyIsActive() {
        assertTrue(PreyHudState.from(ready(myPenalty = true), now).penaltyActive)
        assertFalse(PreyHudState.from(ready(myPenalty = true, myPenaltyEndsAt = "2026-08-31T11:39:00Z"), now).penaltyActive)
        assertFalse(PreyHudState.from(ready(myPenalty = false), now).penaltyActive)
    }

    @Test
    fun from_ShouldReportPenaltyUntilItsEnd_WhenSnapshotCarriesTheEndTime() {
        assertTrue(PreyHudState.from(ready(myPenalty = true, myPenaltyEndsAt = "2026-08-31T11:44:00Z"), now).penaltyActive)
        assertFalse(PreyHudState.from(ready(myPenalty = true, myPenaltyEndsAt = "2026-08-31T11:40:00Z"), now).penaltyActive)
    }
}
