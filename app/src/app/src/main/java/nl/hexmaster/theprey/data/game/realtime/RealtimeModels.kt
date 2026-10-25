package nl.hexmaster.theprey.data.game.realtime

import kotlinx.serialization.KSerializer
import kotlinx.serialization.Serializable
import kotlinx.serialization.SerializationException
import kotlinx.serialization.json.Json
import kotlinx.serialization.json.JsonElement
import kotlinx.serialization.json.JsonNull
import kotlinx.serialization.json.JsonObject
import kotlinx.serialization.json.JsonPrimitive
import kotlinx.serialization.json.booleanOrNull
import kotlinx.serialization.json.buildJsonObject
import kotlinx.serialization.json.contentOrNull
import kotlinx.serialization.json.intOrNull
import kotlinx.serialization.json.jsonObject
import kotlinx.serialization.json.jsonPrimitive
import kotlinx.serialization.json.put

/**
 * Wire constants of the realtime game protocol, mirroring the server's `RealtimeProtocol`
 * (`server/src/Shared/HexMaster.ThePrey.IntegrationEvents/RealtimeProtocol.cs`). See docs/api/realtime.md.
 */
object RealtimeProtocol {
    /** The protocol major version this client understands. */
    const val VERSION = 1

    /** The Web PubSub subprotocol the socket is opened with. */
    const val SUBPROTOCOL = "json.webpubsub.azure.v1"

    object MessageTypes {
        const val PARTICIPANT_JOINED = "participant-joined"
        const val PARTICIPANT_CHANGED = "participant-changed"
        const val PARTICIPANT_REMOVED = "participant-removed"
        const val CONFIGURATION_CHANGED = "configuration-changed"
        const val LOCATIONS_UPDATED = "locations-updated"
        const val PREY_UPDATED = "prey-updated"
        const val GAME_ENDED = "game-ended"
        const val RESYNC_REQUESTED = "resync-requested"
    }

    object PreyEvents {
        const val TAGGED = "tagged"
        const val PENALIZED = "penalized"
        const val PENALTY_CLEARED = "penalty-cleared"
    }
}

/** One versioned group message: `{ v, type, gameId, seq, data }`. */
@Serializable
data class RealtimeEnvelope(
    val v: Int,
    val type: String,
    val gameId: String = "",
    /** Monotonic per game; null only for a server that does not stamp it. */
    val seq: Long? = null,
    val data: JsonElement = JsonNull,
)

/** `participant-removed` payload. */
@Serializable
data class ParticipantRemovedPayload(val userId: String)

/** One entry of a `locations-updated` batch. [role] is `Hunter` or `Prey`. */
@Serializable
data class ParticipantLocationPayload(
    val userId: String,
    val role: String = "",
    val latitude: Double,
    val longitude: Double,
    val state: String,
)

/** `locations-updated` payload. */
@Serializable
data class LocationsUpdatedPayload(val locations: List<ParticipantLocationPayload> = emptyList())

/** `prey-updated` payload; [event] is one of [RealtimeProtocol.PreyEvents]. */
@Serializable
data class PreyUpdatedPayload(
    val userId: String,
    val event: String,
    val state: String? = null,
    val penaltyEndsAt: String? = null,
    val reason: String? = null,
)

/** `game-ended` payload. Emitted exactly once per game. */
@Serializable
data class GameEndedPayload(
    val outcome: String,
    val survivorCount: Int = 0,
    val completedAt: String? = null,
)

/** A frame received on the Web PubSub socket, reduced to what the client acts on. */
sealed interface WebPubSubFrame {
    /** A game message (an envelope) delivered to the group. */
    data class Message(val envelope: RealtimeEnvelope) : WebPubSubFrame

    /** The answer to a request carrying [ackId]; [errorName] is set when not successful. */
    data class Ack(val ackId: Int, val success: Boolean, val errorName: String?) : WebPubSubFrame

    /** System frames (connected, disconnected) and anything else. */
    data object Other : WebPubSubFrame
}

/** Parses Web PubSub `json.webpubsub.azure.v1` frames and builds the one frame the client sends. */
class WebPubSubFrames(private val json: Json = Json { ignoreUnknownKeys = true }) {

    /**
     * Parses one text frame. Messages from the group (or from the service on the server's behalf,
     * which is how REST group broadcasts can arrive) carry an envelope as JSON, or as text holding
     * JSON. Anything unreadable is [WebPubSubFrame.Other].
     */
    fun parse(text: String): WebPubSubFrame = try {
        val frame = json.parseToJsonElement(text).jsonObject
        when (frame["type"]?.jsonPrimitive?.contentOrNull) {
            "message" -> {
                val from = frame["from"]?.jsonPrimitive?.contentOrNull
                val data = frame["data"]
                if ((from == "group" || from == "server") && data != null) {
                    val payload = if (data is JsonPrimitive && data.isString) json.parseToJsonElement(data.content) else data
                    WebPubSubFrame.Message(json.decodeFromJsonElement(RealtimeEnvelope.serializer(), payload))
                } else {
                    WebPubSubFrame.Other
                }
            }
            "ack" -> WebPubSubFrame.Ack(
                ackId = frame["ackId"]?.jsonPrimitive?.intOrNull ?: -1,
                success = frame["success"]?.jsonPrimitive?.booleanOrNull ?: false,
                errorName = (frame["error"] as? JsonObject)?.get("name")?.jsonPrimitive?.contentOrNull,
            )
            else -> WebPubSubFrame.Other
        }
    } catch (_: SerializationException) {
        WebPubSubFrame.Other
    } catch (_: IllegalArgumentException) {
        WebPubSubFrame.Other
    }

    /** `{"type":"joinGroup","group":gameId,"ackId":ackId}`: the group name is the game id. */
    fun joinGroup(gameId: String, ackId: Int): String = buildJsonObject {
        put("type", "joinGroup")
        put("group", gameId)
        put("ackId", ackId)
    }.toString()

    /** Decodes an envelope's [RealtimeEnvelope.data] as [T], or null when it does not match. */
    fun <T> payload(envelope: RealtimeEnvelope, serializer: KSerializer<T>): T? = try {
        json.decodeFromJsonElement(serializer, envelope.data)
    } catch (_: SerializationException) {
        null
    } catch (_: IllegalArgumentException) {
        null
    }

    companion object {
        /** A successful join, or a duplicate join of a group the connection is already in. */
        fun isJoined(ack: WebPubSubFrame.Ack): Boolean = ack.success || ack.errorName == "Duplicate"
    }
}

