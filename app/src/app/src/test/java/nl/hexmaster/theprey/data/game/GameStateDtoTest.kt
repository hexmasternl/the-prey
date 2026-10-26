package nl.hexmaster.theprey.data.game

import kotlinx.serialization.json.Json
import kotlinx.serialization.json.jsonObject
import kotlinx.serialization.json.jsonPrimitive
import nl.hexmaster.theprey.data.playfield.GpsCoordinateDto
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
                ParticipantDto(
                    "6f1c1d0e-8f43-4a8e-9a43-3d8b0e1f2a11",
                    PlayerStates.ACTIVE,
                    displayName = "Nina",
                    isReady = true,
                    lastKnownLocation = GpsCoordinateDto(52.36, 4.87),
                ),
                ParticipantDto("0b5e2a8c-0000-4000-8000-000000000002", PlayerStates.TAGGED, displayName = "Rex", isReady = true),
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

    @Test
    fun fullGameStateDto_ShouldReadTheOutcome_WhenPayloadIsACompletedGame() {
        val payload = """
            {"game":{"id":"g","gameCode":"4291","ownerUserId":"owner","status":"Completed","outcome":"HuntersWin",
              "completedAt":"2026-08-31T12:10:00+00:00",
              "configuration":{"gameDuration":30,"hunterDelayTime":5,"finalStageDuration":10,
                "defaultLocationInterval":180,"finalLocationInterval":60},
              "participants":[{"userId":"owner","displayName":"Fox","isReady":true,"state":"Active",
                "lastKnownLocation":null,"hasActivePenalty":false},
                {"userId":"p1","displayName":"Hare","isReady":true,"state":"Tagged",
                "lastKnownLocation":null,"hasActivePenalty":false}],
              "hunterUserId":"owner","isOwnerPlayer":true,"isReadyToStart":false},
             "playfieldName":"Vondelpark","playfieldCoordinates":[],"status":null,"roleState":null,
             "serverTime":"2026-08-31T12:10:01+00:00"}
        """.trimIndent()

        val state = json.decodeFromString(FullGameStateDto.serializer(), payload)

        assertEquals(GameStatuses.COMPLETED, state.game.status)
        assertEquals(GameOutcomes.HUNTERS_WIN, state.game.outcome)
        assertEquals(PlayerStates.TAGGED, state.game.participants.last().state)
    }

    @Test
    fun fullGameStateDto_ShouldDeserializeLobbyFields_WhenPayloadIsALobbyGame() {
        val payload = """
            {"game":{"id":"g","gameCode":"4291","ownerUserId":"owner","status":"Lobby","outcome":"None",
              "configuration":{"gameDuration":30,"hunterDelayTime":5,"finalStageDuration":10,
                "defaultLocationInterval":180,"finalLocationInterval":60,
                "enablePreyBoundaryPenalties":true,"enableHunterBoundaryPenalty":false},
              "participants":[{"userId":"owner","displayName":"Fox","isReady":false,"state":"Active",
                "lastKnownLocation":null,"hasActivePenalty":false}],
              "hunterUserId":null,"isOwnerPlayer":true,"isReadyToStart":false},
             "playfieldName":"Vondelpark","playfieldCoordinates":[],"status":null,"roleState":null,
             "serverTime":"2026-08-31T11:40:00+00:00"}
        """.trimIndent()

        val state = json.decodeFromString(FullGameStateDto.serializer(), payload)

        assertEquals("4291", state.game.gameCode)
        assertEquals("owner", state.game.ownerUserId)
        assertEquals(GameStatuses.LOBBY, state.game.status)
        assertEquals(GameOutcomes.NONE, state.game.outcome)
        assertEquals(
            GameConfigurationDto(30, 5, 10, 180, 60, enablePreyBoundaryPenalties = true, enableHunterBoundaryPenalty = false),
            state.game.configuration,
        )
        assertEquals(ParticipantDto("owner", PlayerStates.ACTIVE, displayName = "Fox"), state.game.participants.single())
        assertEquals("Vondelpark", state.playfieldName)
    }

    private fun inProgress(status: String, roleState: String) = """
        {"game":{"id":"g","status":"InProgress","ownerUserId":"o","hunterUserId":"h",
          "configuration":{"gameDuration":60,"hunterDelayTime":5,"finalStageDuration":10,
            "defaultLocationInterval":180,"finalLocationInterval":60},
          "participants":[
            {"userId":"h","displayName":"Hunter","isReady":true,"state":"Active",
             "lastKnownLocation":{"latitude":52.1,"longitude":5.1},"hasActivePenalty":false},
            {"userId":"p","displayName":"Prey","isReady":true,"state":"Active",
             "lastKnownLocation":null,"hasActivePenalty":true}]},
         "playfieldName":"Vondelpark",
         "playfieldCoordinates":[{"latitude":52.0,"longitude":5.0},{"latitude":52.0,"longitude":5.2},{"latitude":52.2,"longitude":5.1}],
         "status":$status,"roleState":$roleState,"serverTime":"2026-08-31T11:40:00+00:00"}
    """.trimIndent()

    @Test
    fun fullGameStateDto_ShouldReadStatusAndPreyRoleState_WhenPreySnapshotIsInProgress() {
        val state = json.decodeFromString(
            FullGameStateDto.serializer(),
            inProgress(
                """{"gameId":"g","playfieldName":"Vondelpark","playfieldCoordinates":[],"hunterUserId":"h","participants":[],
                   "gameDurationLeft":754,"nextPingDuration":18,"nextPingDurationWithPenalty":0,"currentPingInterval":30,
                   "isEndgame":false,"preysLeft":4,"hunterMayMoveAt":"2026-08-31T11:43:20+00:00"}""",
                """{"hunterDistanceMeters":120,"preyLocations":[]}""",
            ),
        )

        assertEquals(3, state.playfieldCoordinates.size)
        assertEquals(GameStatusDto(754, 18, 30, false, 4, "2026-08-31T11:43:20+00:00"), state.status)
        assertEquals(120, state.roleState?.hunterDistanceMeters)
        assertTrue(state.game.participants[1].hasActivePenalty)
        assertEquals(GpsCoordinateDto(52.1, 5.1), state.game.participants[0].lastKnownLocation)
    }

    @Test
    fun fullGameStateDto_ShouldReadPreyLocations_WhenHunterSnapshotIsInProgress() {
        val state = json.decodeFromString(
            FullGameStateDto.serializer(),
            inProgress(
                """{"gameDurationLeft":600,"nextPingDuration":5,"currentPingInterval":180,"preysLeft":1,"hunterMayMoveAt":null}""",
                """{"hunterDistanceMeters":null,"preyLocations":[{"latitude":52.15,"longitude":5.12}]}""",
            ),
        )

        assertNull(state.roleState?.hunterDistanceMeters)
        assertEquals(listOf(GpsCoordinateDto(52.15, 5.12)), state.roleState?.preyLocations)
        assertNull(state.status?.hunterMayMoveAt)
    }

    @Test
    fun fullGameStateDto_ShouldHaveNoSlices_WhenGameIsStartedAndStatusIsNull() {
        val state = json.decodeFromString(
            FullGameStateDto.serializer(),
            inProgress("null", "null").replace("\"InProgress\"", "\"Started\""),
        )

        assertEquals(GameStatuses.STARTED, state.game.status)
        assertNull(state.status)
        assertNull(state.roleState)
    }

    @Test
    fun fullGameStateDto_ShouldReadPenaltyEndsAt_WhenSnapshotParticipantIsPenalized() {
        val payload = inProgress("null", "null").replace(
            "\"hasActivePenalty\":true}",
            "\"hasActivePenalty\":true,\"penaltyEndsAt\":\"2026-08-31T11:44:00+00:00\"}",
        )

        val prey = json.decodeFromString(FullGameStateDto.serializer(), payload).game.participants[1]

        assertTrue(prey.hasActivePenalty)
        assertEquals("2026-08-31T11:44:00+00:00", prey.penaltyEndsAt)
    }

    @Test
    fun fullGameStateDto_ShouldHaveNoPenaltyEnd_WhenOlderServerOmitsIt() {
        val prey = json.decodeFromString(FullGameStateDto.serializer(), inProgress("null", "null")).game.participants[1]

        assertTrue(prey.hasActivePenalty)
        assertNull(prey.penaltyEndsAt)
    }
}
