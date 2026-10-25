package nl.hexmaster.theprey.data.game

import kotlinx.serialization.json.Json
import kotlinx.serialization.json.jsonObject
import kotlinx.serialization.json.jsonPrimitive
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

/** Decoding against payloads shaped like the server's DTOs (see docs/api/endpoints.md). */
class GameStateDtoTest {

    private val json = Json { ignoreUnknownKeys = true }

    @Test
    fun fullGameStateDto_ShouldDeserialize_WhenPayloadIsAnInProgressGameState() {
        val payload = """
            {"game":{"id":"7c9e6679-7425-40de-944b-e07fc1f90ae7","gameCode":"4291",
              "playfieldId":"3fa85f64-5717-4562-b3fc-2c963f66afa6",
              "ownerUserId":"6f1c1d0e-8f43-4a8e-9a43-3d8b0e1f2a11","status":"InProgress",
              "configuration":{"gameDuration":60,"hunterDelayTime":5,"finalStageDuration":10,
                "defaultLocationInterval":30,"finalLocationInterval":10,
                "enablePreyBoundaryPenalties":false,"enableHunterBoundaryPenalty":false},
              "participants":[
                {"userId":"6f1c1d0e-8f43-4a8e-9a43-3d8b0e1f2a11","displayName":"Nina","profilePictureUrl":null,
                 "isReady":true,"state":"Active","lastKnownLocation":{"latitude":52.36,"longitude":4.87},"hasActivePenalty":false},
                {"userId":"0b5e2a8c-0000-4000-8000-000000000002","displayName":"Rex","profilePictureUrl":null,
                 "isReady":true,"state":"Tagged","lastKnownLocation":null,"hasActivePenalty":false}],
              "hunterUserId":"0b5e2a8c-0000-4000-8000-000000000003",
              "preys":["6f1c1d0e-8f43-4a8e-9a43-3d8b0e1f2a11"],
              "startedAt":"2026-08-31T11:00:00+00:00","createdAt":"2026-08-31T10:50:00+00:00",
              "endsAt":"2026-08-31T12:00:00.1234567+00:00","cleanUpAfter":"2026-09-01T12:00:00+00:00",
              "outcome":"None","completedAt":null,"isOwnerPlayer":true,"isReadyToStart":false},
             "playfieldName":"Vondelpark","playfieldCoordinates":[{"latitude":52.3702,"longitude":4.8952}],
             "status":{"gameDurationLeft":1200},"roleState":{"hunterDistanceMeters":120,"preyLocations":[]},
             "serverTime":"2026-08-31T11:40:00+00:00"}
        """.trimIndent()

        val state = json.decodeFromString(FullGameStateDto.serializer(), payload)

        assertEquals("7c9e6679-7425-40de-944b-e07fc1f90ae7", state.game.id)
        assertEquals(GameStatuses.IN_PROGRESS, state.game.status)
        assertEquals(60, state.game.configuration.gameDuration)
        assertEquals("2026-08-31T12:00:00.1234567+00:00", state.game.endsAt)
        assertEquals("2026-08-31T11:40:00+00:00", state.serverTime)
        assertEquals(
            listOf(
                ParticipantDto("6f1c1d0e-8f43-4a8e-9a43-3d8b0e1f2a11", "Active"),
                ParticipantDto("0b5e2a8c-0000-4000-8000-000000000002", PlayerStates.TAGGED),
            ),
            state.game.participants,
        )
    }

    @Test
    fun fullGameStateDto_ShouldHaveNoEndTime_WhenGameIsStarted() {
        val payload = """
            {"game":{"id":"g","status":"Started","configuration":{"gameDuration":30},"participants":[],
              "endsAt":null},"status":null,"roleState":null,"serverTime":"2026-08-31T11:00:00Z"}
        """.trimIndent()

        val state = json.decodeFromString(FullGameStateDto.serializer(), payload)

        assertEquals(GameStatuses.STARTED, state.game.status)
        assertNull(state.game.endsAt)
    }

    @Test
    fun recordLocationResponse_ShouldDeserialize_WhenPayloadMatchesTheDocumentedAck() {
        val payload = """
            {"accepted":true,"nextLocationIntervalSeconds":5,"penaltyIntervalSeconds":null,
             "penaltyEndsAt":null,"keepReporting":false}
        """.trimIndent()

        val ack = json.decodeFromString(RecordLocationResponse.serializer(), payload)

        assertTrue(ack.accepted)
        assertEquals(5, ack.nextLocationIntervalSeconds)
        assertFalse(ack.keepReporting)
    }

    @Test
    fun recordLocationResponse_ShouldKeepReporting_WhenServerOmitsTheFlag() {
        val ack = json.decodeFromString(
            RecordLocationResponse.serializer(),
            """{"accepted":true,"nextLocationIntervalSeconds":10}""",
        )

        assertTrue(ack.keepReporting)
    }

    @Test
    fun recordLocationRequest_ShouldUseTheServerFieldNames_WhenEncoded() {
        val body = json.encodeToString(
            RecordLocationRequest.serializer(),
            RecordLocationRequest(52.3702, 4.8952, "2026-06-01T14:00:00Z", 5.0),
        )

        val fields = json.parseToJsonElement(body).jsonObject
        assertEquals(setOf("latitude", "longitude", "recordedAt", "accuracy"), fields.keys)
        assertEquals("2026-06-01T14:00:00Z", fields.getValue("recordedAt").jsonPrimitive.content)
    }
}
