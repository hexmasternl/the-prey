package nl.hexmaster.theprey.data.game.realtime

import nl.hexmaster.theprey.data.game.FullGameStateDto
import nl.hexmaster.theprey.data.game.GameDto
import nl.hexmaster.theprey.data.game.GameStatuses
import nl.hexmaster.theprey.data.game.ParticipantDto
import nl.hexmaster.theprey.data.game.PlayerStates
import nl.hexmaster.theprey.data.game.realtime.RealtimeProtocol.MessageTypes
import nl.hexmaster.theprey.data.game.realtime.RealtimeProtocol.PreyEvents
import nl.hexmaster.theprey.data.playfield.GpsCoordinateDto

/** What applying one envelope to the local game state results in. */
sealed interface Reduction {
    /** The new state and the `seq` it is now at. Unknown message types leave the state as it was. */
    data class Applied(val state: FullGameStateDto, val seq: Long?) : Reduction

    /** The message cannot be applied (gap, regression, unknown `v`, unreadable, or asked for): pull a snapshot. */
    data object Resync : Reduction

    /** The current user was removed from the game. */
    data class Removed(val seq: Long?) : Reduction

    /** The game ended; [state] shows it `Completed` with [outcome]. */
    data class Ended(val state: FullGameStateDto, val outcome: String, val seq: Long?) : Reduction

    /** A message for another game; nothing changes. */
    data object Ignored : Reduction
}

/**
 * Applies versioned realtime envelopes to an immutable game state (docs/api/realtime.md). Pure: no
 * clock, no socket. The first message after a snapshot sets the `seq` baseline ([lastSeq] null).
 */
class GameStateReducer(private val frames: WebPubSubFrames = WebPubSubFrames()) {

    fun reduce(state: FullGameStateDto, lastSeq: Long?, myUserId: String?, envelope: RealtimeEnvelope): Reduction {
        if (envelope.v != RealtimeProtocol.VERSION) return Reduction.Resync
        if (envelope.gameId.isNotBlank() && !envelope.gameId.equals(state.game.id, ignoreCase = true)) {
            return Reduction.Ignored
        }
        val seq = envelope.seq
        if (seq != null && lastSeq != null && seq != lastSeq + 1) return Reduction.Resync
        val nextSeq = seq ?: lastSeq
        val game = state.game

        fun applied(newGame: GameDto) = Reduction.Applied(state.copy(game = newGame), nextSeq)

        return when (envelope.type) {
            MessageTypes.PARTICIPANT_JOINED, MessageTypes.PARTICIPANT_CHANGED -> {
                val participant = frames.payload(envelope, ParticipantDto.serializer()) ?: return Reduction.Resync
                applied(game.copy(participants = game.participants.upsert(participant)))
            }
            MessageTypes.PARTICIPANT_REMOVED -> {
                val removed = frames.payload(envelope, ParticipantRemovedPayload.serializer()) ?: return Reduction.Resync
                if (myUserId != null && removed.userId.equals(myUserId, ignoreCase = true)) {
                    Reduction.Removed(nextSeq)
                } else {
                    applied(game.copy(participants = game.participants.filterNot { it.userId.sameAs(removed.userId) }))
                }
            }
            MessageTypes.CONFIGURATION_CHANGED -> {
                val slice = frames.payload(envelope, GameDto.serializer()) ?: return Reduction.Resync
                // A status change alters snapshot-only data (in-progress status, role state, server time).
                if (!slice.status.equals(game.status, ignoreCase = true)) return Reduction.Resync
                // The game-level slice replaces everything except the participant list it does not carry.
                applied(slice.copy(participants = game.participants))
            }
            MessageTypes.LOCATIONS_UPDATED -> {
                val update = frames.payload(envelope, LocationsUpdatedPayload.serializer()) ?: return Reduction.Resync
                val byUser = update.locations.associateBy { it.userId.lowercase() }
                applied(
                    game.copy(
                        participants = game.participants.map { participant ->
                            val location = byUser[participant.userId.lowercase()] ?: return@map participant
                            participant.copy(
                                lastKnownLocation = GpsCoordinateDto(location.latitude, location.longitude),
                                state = location.state,
                            )
                        },
                    ),
                )
            }
            MessageTypes.PREY_UPDATED -> {
                val update = frames.payload(envelope, PreyUpdatedPayload.serializer()) ?: return Reduction.Resync
                applied(
                    game.copy(
                        participants = game.participants.map { participant ->
                            if (!participant.userId.sameAs(update.userId)) return@map participant
                            when (update.event) {
                                PreyEvents.TAGGED -> participant.copy(
                                    state = update.state ?: PlayerStates.TAGGED,
                                    hasActivePenalty = false,
                                    penaltyEndsAt = null,
                                )
                                PreyEvents.PENALIZED -> participant.copy(hasActivePenalty = true, penaltyEndsAt = update.penaltyEndsAt)
                                PreyEvents.PENALTY_CLEARED -> participant.copy(hasActivePenalty = false, penaltyEndsAt = null)
                                else -> participant
                            }
                        },
                    ),
                )
            }
            MessageTypes.GAME_ENDED -> {
                val ended = frames.payload(envelope, GameEndedPayload.serializer()) ?: return Reduction.Resync
                Reduction.Ended(
                    state.copy(game = game.copy(status = GameStatuses.COMPLETED, outcome = ended.outcome)),
                    ended.outcome,
                    nextSeq,
                )
            }
            MessageTypes.RESYNC_REQUESTED -> Reduction.Resync
            // Forward compatible: a newer message type under the same major version is skipped.
            else -> Reduction.Applied(state, nextSeq)
        }
    }

    private fun List<ParticipantDto>.upsert(participant: ParticipantDto): List<ParticipantDto> =
        if (any { it.userId.sameAs(participant.userId) }) {
            map { if (it.userId.sameAs(participant.userId)) participant else it }
        } else {
            this + participant
        }

    private fun String.sameAs(other: String) = equals(other, ignoreCase = true)
}
