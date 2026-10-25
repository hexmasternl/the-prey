package nl.hexmaster.theprey.data.game.realtime

import kotlinx.serialization.json.Json
import kotlinx.serialization.json.int
import kotlinx.serialization.json.jsonObject
import kotlinx.serialization.json.jsonPrimitive
import nl.hexmaster.theprey.data.game.GameDto
import nl.hexmaster.theprey.data.game.GameStatuses
import nl.hexmaster.theprey.data.game.ParticipantDto
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

/** Decoding the samples in docs/api/realtime.md. */
class RealtimeModelsTest {

    private val frames = WebPubSubFrames()

    private fun groupFrame(envelope: String) = """{"type":"message","from":"group","group":"g1","dataType":"json","data":$envelope}"""

    private fun envelopeOf(frame: String): RealtimeEnvelope = (frames.parse(frame) as WebPubSubFrame.Message).envelope

    @Test
    fun parse_ShouldReadEnvelope_WhenGroupMessageCarriesParticipantChanged() {
        val envelope = envelopeOf(
            groupFrame(
                """{"v":1,"type":"participant-changed","gameId":"g1","seq":42,"data":
                {"userId":"u1","displayName":"Fox","profilePictureUrl":null,"isReady":true,"state":"Active",
                 "lastKnownLocation":null,"hasActivePenalty":false}}""",
            ),
        )

        assertEquals(1, envelope.v)
        assertEquals(RealtimeProtocol.MessageTypes.PARTICIPANT_CHANGED, envelope.type)
        assertEquals("g1", envelope.gameId)
        assertEquals(42L, envelope.seq)
        assertEquals(
            ParticipantDto("u1", "Active", displayName = "Fox", isReady = true),
            frames.payload(envelope, ParticipantDto.serializer()),
        )
    }

    @Test
    fun parse_ShouldReadEnvelope_WhenDataIsTextHoldingJson() {
        val frame = """{"type":"message","from":"server","dataType":"text",
            "data":"{\"v\":1,\"type\":\"resync-requested\",\"gameId\":\"g1\",\"seq\":3,\"data\":{\"reason\":\"x\"}}"}"""

        assertEquals(RealtimeProtocol.MessageTypes.RESYNC_REQUESTED, envelopeOf(frame).type)
    }

    @Test
    fun payload_ShouldReadConfigurationChangedAsGameSlice_WhenParticipantsAreAbsent() {
        val envelope = envelopeOf(
            groupFrame(
                """{"v":1,"type":"configuration-changed","gameId":"g1","seq":5,"data":
                {"id":"g1","gameCode":"ABCD","playfieldId":"p","ownerUserId":"o","status":"InProgress",
                 "configuration":{"gameDuration":60,"hunterDelayTime":5,"finalStageDuration":10,
                   "defaultLocationInterval":180,"finalLocationInterval":60,
                   "enablePreyBoundaryPenalties":false,"enableHunterBoundaryPenalty":false},
                 "hunterUserId":"h","preys":["p1"],"startedAt":null,"createdAt":"2026-07-17T11:00:00Z",
                 "endsAt":null,"cleanUpAfter":"2026-07-19T11:00:00Z","outcome":"None","completedAt":null}}""",
            ),
        )

        val game = frames.payload(envelope, GameDto.serializer())!!
        assertEquals(GameStatuses.IN_PROGRESS, game.status)
        assertEquals("o", game.ownerUserId)
        assertEquals("h", game.hunterUserId)
        assertTrue(game.participants.isEmpty())
    }

    @Test
    fun payload_ShouldReadGameplayPayloads_WhenSamplesFromTheDocs() {
        val locations = frames.payload(
            envelopeOf(
                groupFrame(
                    """{"v":1,"type":"locations-updated","gameId":"g1","seq":6,"data":{"locations":[
                    {"userId":"u1","role":"Prey","latitude":51.9,"longitude":4.4,"state":"Active"}]}}""",
                ),
            ),
            LocationsUpdatedPayload.serializer(),
        )!!
        assertEquals(ParticipantLocationPayload("u1", "Prey", 51.9, 4.4, "Active"), locations.locations.single())

        val prey = frames.payload(
            envelopeOf(
                groupFrame(
                    """{"v":1,"type":"prey-updated","gameId":"g1","seq":7,"data":{"userId":"u1","event":"penalized",
                    "state":null,"penaltyEndsAt":"2026-07-17T12:00:00Z","reason":"left-playfield"}}""",
                ),
            ),
            PreyUpdatedPayload.serializer(),
        )!!
        assertEquals(PreyUpdatedPayload("u1", "penalized", null, "2026-07-17T12:00:00Z", "left-playfield"), prey)

        val ended = frames.payload(
            envelopeOf(
                groupFrame(
                    """{"v":1,"type":"game-ended","gameId":"g1","seq":8,"data":
                    {"outcome":"PreyEscaped","survivorCount":2,"completedAt":"2026-07-17T12:30:00Z"}}""",
                ),
            ),
            GameEndedPayload.serializer(),
        )!!
        assertEquals(GameEndedPayload("PreyEscaped", 2, "2026-07-17T12:30:00Z"), ended)

        val removed = frames.payload(
            envelopeOf(groupFrame("""{"v":1,"type":"participant-removed","gameId":"g1","seq":9,"data":{"userId":"u2"}}""")),
            ParticipantRemovedPayload.serializer(),
        )
        assertEquals(ParticipantRemovedPayload("u2"), removed)
    }

    @Test
    fun parse_ShouldReadAck_WhenJoinSucceedsOrIsDuplicate() {
        val ok = frames.parse("""{"type":"ack","ackId":1,"success":true}""") as WebPubSubFrame.Ack
        val duplicate = frames.parse(
            """{"type":"ack","ackId":1,"success":false,"error":{"name":"Duplicate","message":"already"}}""",
        ) as WebPubSubFrame.Ack
        val forbidden = frames.parse(
            """{"type":"ack","ackId":1,"success":false,"error":{"name":"Forbidden","message":"no"}}""",
        ) as WebPubSubFrame.Ack

        assertTrue(WebPubSubFrames.isJoined(ok))
        assertTrue(WebPubSubFrames.isJoined(duplicate))
        assertFalse(WebPubSubFrames.isJoined(forbidden))
        assertEquals(1, ok.ackId)
    }

    @Test
    fun parse_ShouldReturnOther_WhenFrameIsSystemOrUnreadable() {
        assertEquals(WebPubSubFrame.Other, frames.parse("""{"type":"system","event":"connected","connectionId":"c"}"""))
        assertEquals(WebPubSubFrame.Other, frames.parse("not json"))
        assertEquals(WebPubSubFrame.Other, frames.parse("""{"type":"message","from":"group","data":{"nope":1}}"""))
    }

    @Test
    fun joinGroup_ShouldBuildControlFrame_WhenJoiningTheGameGroup() {
        val frame = Json.parseToJsonElement(frames.joinGroup("g1", 1)).jsonObject

        assertEquals("joinGroup", frame.getValue("type").jsonPrimitive.content)
        assertEquals("g1", frame.getValue("group").jsonPrimitive.content)
        assertEquals(1, frame.getValue("ackId").jsonPrimitive.int)
    }
}
