package nl.hexmaster.theprey.data.game.realtime

import kotlinx.serialization.json.Json
import kotlinx.serialization.json.JsonElement
import kotlinx.serialization.json.JsonObject
import nl.hexmaster.theprey.data.game.FullGameStateDto
import nl.hexmaster.theprey.data.game.GameConfigurationDto
import nl.hexmaster.theprey.data.game.GameDto
import nl.hexmaster.theprey.data.game.GameOutcomes
import nl.hexmaster.theprey.data.game.GameStatuses
import nl.hexmaster.theprey.data.game.ParticipantDto
import nl.hexmaster.theprey.data.game.PlayerStates
import nl.hexmaster.theprey.data.playfield.GpsCoordinateDto
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

class GameStateReducerTest {

    private val reducer = GameStateReducer()
    private val me = "me"

    private val state = FullGameStateDto(
        game = GameDto(
            id = "g1",
            status = GameStatuses.LOBBY,
            configuration = GameConfigurationDto(30, 5, 10, 180, 60),
            participants = listOf(
                ParticipantDto("owner", PlayerStates.ACTIVE, displayName = "Owner"),
                ParticipantDto(me, PlayerStates.ACTIVE, displayName = "Me"),
            ),
            ownerUserId = "owner",
        ),
        serverTime = "2026-08-31T11:40:00+00:00",
    )

    private fun envelope(type: String, data: String = "{}", seq: Long? = 1, v: Int = 1, gameId: String = "g1") =
        RealtimeEnvelope(v = v, type = type, gameId = gameId, seq = seq, data = Json.parseToJsonElement(data))

    private fun reduce(envelope: RealtimeEnvelope, lastSeq: Long? = null) = reducer.reduce(state, lastSeq, me, envelope)

    private fun Reduction.applied(): Reduction.Applied = this as Reduction.Applied

    private fun participant(id: String, ready: Boolean = false) =
        """{"userId":"$id","displayName":"P $id","isReady":$ready,"state":"Active","lastKnownLocation":null,"hasActivePenalty":false}"""

    // ---- participants ----

    @Test
    fun reduce_ShouldAppendParticipant_WhenParticipantJoins() {
        val result = reduce(envelope(RealtimeProtocol.MessageTypes.PARTICIPANT_JOINED, participant("new"))).applied()

        assertEquals(listOf("owner", me, "new"), result.state.game.participants.map { it.userId })
        assertEquals(1L, result.seq)
    }

    @Test
    fun reduce_ShouldReplaceParticipantWholesale_WhenParticipantChanges() {
        val result = reduce(envelope(RealtimeProtocol.MessageTypes.PARTICIPANT_CHANGED, participant(me, ready = true))).applied()

        assertEquals(ParticipantDto(me, PlayerStates.ACTIVE, displayName = "P me", isReady = true), result.state.game.participants[1])
        assertEquals(2, result.state.game.participants.size)
    }

    @Test
    fun reduce_ShouldDropParticipant_WhenAnotherParticipantIsRemoved() {
        val result = reduce(envelope(RealtimeProtocol.MessageTypes.PARTICIPANT_REMOVED, """{"userId":"owner"}""")).applied()

        assertEquals(listOf(me), result.state.game.participants.map { it.userId })
    }

    @Test
    fun reduce_ShouldReportRemoved_WhenCurrentUserIsRemoved() {
        assertEquals(Reduction.Removed(1L), reduce(envelope(RealtimeProtocol.MessageTypes.PARTICIPANT_REMOVED, """{"userId":"ME"}""")))
    }

    // ---- configuration ----

    @Test
    fun reduce_ShouldApplyConfiguration_WhenStatusIsUnchanged() {
        val game = reduce(envelope(RealtimeProtocol.MessageTypes.CONFIGURATION_CHANGED, slice("Lobby"))).applied().state.game

        assertEquals(GameStatuses.LOBBY, game.status)
        assertEquals(60, game.configuration.gameDuration)
        assertEquals(me, game.hunterUserId)
        assertEquals(state.game.participants, game.participants)
    }

    @Test
    fun reduce_ShouldResync_WhenConfigurationChangedPromotesStartedToInProgress() {
        val started = state.copy(game = state.game.copy(status = GameStatuses.STARTED))

        val result = reducer.reduce(started, null, me, envelope(RealtimeProtocol.MessageTypes.CONFIGURATION_CHANGED, slice("InProgress")))

        assertEquals(Reduction.Resync, result)
    }

    @Test
    fun reduce_ShouldResync_WhenConfigurationChangedMovesLobbyToReady() {
        assertEquals(Reduction.Resync, reduce(envelope(RealtimeProtocol.MessageTypes.CONFIGURATION_CHANGED, slice("Ready"))))
    }

    private fun slice(status: String) = """{"id":"g1","gameCode":"4291","ownerUserId":"owner","status":"$status",
        "configuration":{"gameDuration":60,"hunterDelayTime":3,"finalStageDuration":5,
          "defaultLocationInterval":120,"finalLocationInterval":60,
          "enablePreyBoundaryPenalties":false,"enableHunterBoundaryPenalty":false},
        "hunterUserId":"me","preys":["owner"],"outcome":"None"}"""

    // ---- game end ----

    @Test
    fun reduce_ShouldEndAsCancelled_WhenOwnerAbandonedTheLobby() {
        val result = reduce(envelope(RealtimeProtocol.MessageTypes.GAME_ENDED, """{"outcome":"Cancelled","survivorCount":0}"""))

        result as Reduction.Ended
        assertEquals(GameOutcomes.CANCELLED, result.outcome)
        assertEquals(GameStatuses.COMPLETED, result.state.game.status)
    }

    @Test
    fun reduce_ShouldEndAsCompleted_WhenGameWasPlayedOut() {
        val result = reduce(envelope(RealtimeProtocol.MessageTypes.GAME_ENDED, """{"outcome":"PreyEscaped","survivorCount":2}"""))

        result as Reduction.Ended
        assertEquals("PreyEscaped", result.outcome)
        assertEquals("PreyEscaped", result.state.game.outcome)
    }

    // ---- gameplay ----

    @Test
    fun reduce_ShouldOverlayLocationsOfNamedParticipantsOnly_WhenLocationsUpdate() {
        val data = """{"locations":[{"userId":"owner","role":"Hunter","latitude":52.1,"longitude":5.1,"state":"Active"}]}"""

        val participants = reduce(envelope(RealtimeProtocol.MessageTypes.LOCATIONS_UPDATED, data)).applied().state.game.participants

        assertEquals(GpsCoordinateDto(52.1, 5.1), participants[0].lastKnownLocation)
        assertEquals(state.game.participants[1], participants[1])
    }

    @Test
    fun reduce_ShouldApplyPreyEvents_WhenPreyIsPenalizedClearedAndTagged() {
        val penalized = reduce(
            envelope(
                RealtimeProtocol.MessageTypes.PREY_UPDATED,
                """{"userId":"me","event":"penalized","penaltyEndsAt":"2026-07-17T12:00:00Z","reason":"left-playfield"}""",
            ),
        ).applied().state
        assertTrue(penalized.game.participants[1].hasActivePenalty)
        assertEquals("2026-07-17T12:00:00Z", penalized.game.participants[1].penaltyEndsAt)

        val cleared = reducer.reduce(
            penalized, 1, me, envelope(RealtimeProtocol.MessageTypes.PREY_UPDATED, """{"userId":"me","event":"penalty-cleared"}""", seq = 2),
        ).applied().state
        assertEquals(false, cleared.game.participants[1].hasActivePenalty)

        val tagged = reducer.reduce(
            cleared, 2, me, envelope(RealtimeProtocol.MessageTypes.PREY_UPDATED, """{"userId":"me","event":"tagged","state":"Tagged"}""", seq = 3),
        ).applied().state
        assertEquals(PlayerStates.TAGGED, tagged.game.participants[1].state)
    }

    // ---- protocol ----

    @Test
    fun reduce_ShouldResync_WhenResyncIsRequested() {
        assertEquals(Reduction.Resync, reduce(envelope(RealtimeProtocol.MessageTypes.RESYNC_REQUESTED, """{"reason":"x"}""")))
    }

    @Test
    fun reduce_ShouldResync_WhenVersionIsUnsupported() {
        assertEquals(Reduction.Resync, reduce(envelope(RealtimeProtocol.MessageTypes.PARTICIPANT_JOINED, participant("x"), v = 2)))
    }

    @Test
    fun reduce_ShouldResync_WhenSeqHasAGap() {
        assertEquals(Reduction.Resync, reduce(envelope(RealtimeProtocol.MessageTypes.PARTICIPANT_JOINED, participant("x"), seq = 7), lastSeq = 5))
    }

    @Test
    fun reduce_ShouldResync_WhenSeqRegressesOrRepeats() {
        assertEquals(Reduction.Resync, reduce(envelope(RealtimeProtocol.MessageTypes.PARTICIPANT_JOINED, participant("x"), seq = 5), lastSeq = 5))
        assertEquals(Reduction.Resync, reduce(envelope(RealtimeProtocol.MessageTypes.PARTICIPANT_JOINED, participant("x"), seq = 2), lastSeq = 5))
    }

    @Test
    fun reduce_ShouldApplyNextSeq_WhenSeqIsContiguous() {
        val result = reduce(envelope(RealtimeProtocol.MessageTypes.PARTICIPANT_JOINED, participant("x"), seq = 6), lastSeq = 5).applied()

        assertEquals(6L, result.seq)
    }

    @Test
    fun reduce_ShouldSetBaseline_WhenFirstMessageAfterSnapshot() {
        val result = reduce(envelope(RealtimeProtocol.MessageTypes.PARTICIPANT_JOINED, participant("x"), seq = 41), lastSeq = null).applied()

        assertEquals(41L, result.seq)
    }

    @Test
    fun reduce_ShouldIgnoreTypeButAdvanceSeq_WhenTypeIsUnknown() {
        val result = reduce(envelope("future-thing", "{}", seq = 6), lastSeq = 5).applied()

        assertEquals(state, result.state)
        assertEquals(6L, result.seq)
    }

    @Test
    fun reduce_ShouldIgnore_WhenMessageIsForAnotherGame() {
        assertEquals(Reduction.Ignored, reduce(envelope(RealtimeProtocol.MessageTypes.PARTICIPANT_JOINED, participant("x"), gameId = "other")))
    }

    @Test
    fun reduce_ShouldResync_WhenPayloadDoesNotMatchItsType() {
        val data: JsonElement = JsonObject(emptyMap())
        assertEquals(Reduction.Resync, reduce(RealtimeEnvelope(1, RealtimeProtocol.MessageTypes.PARTICIPANT_REMOVED, "g1", 1, data)))
    }
}
