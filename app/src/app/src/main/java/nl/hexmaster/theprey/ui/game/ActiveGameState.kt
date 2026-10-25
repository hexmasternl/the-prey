package nl.hexmaster.theprey.ui.game

import nl.hexmaster.theprey.data.game.FullGameStateDto
import nl.hexmaster.theprey.data.game.GameStatuses
import nl.hexmaster.theprey.data.location.GeoPosition

/** The signed-in player's role in the game on screen. */
enum class GameRole { Hunter, Prey, None }

/** `Starting`: the game is `Started` but its clock does not run yet. `Playing`: `InProgress`. */
enum class GamePhase { Starting, Playing }

/** One participant as the game view uses them. */
data class GameParticipant(
    val userId: String,
    val callsign: String,
    /** `Active`, `Passive`, `Tagged` or `Out`. */
    val state: String,
    val location: GeoPosition?,
    val isHunter: Boolean,
    val hasActivePenalty: Boolean,
    val penaltyEndsAt: String?,
    val isMe: Boolean,
)

/** The game on screen, derived from one snapshot of the Game State Service. Immutable. */
data class ActiveGameState(
    val gameId: String,
    val role: GameRole,
    val phase: GamePhase,
    /** The playfield polygon; empty when the server could not resolve it. */
    val area: List<GeoPosition>,
    /** When the hunter may move (ISO-8601); null when unknown. */
    val hunterMayMoveAt: String?,
    /** The snapshot's server clock reading (ISO-8601). */
    val serverTime: String,
    val participants: List<GameParticipant>,
) {
    companion object {
        /**
         * The hunter is the participant whose id is the game's hunter id; every other participant
         * is a prey; anyone else watches as [GameRole.None].
         */
        fun from(snapshot: FullGameStateDto, myUserId: String?): ActiveGameState {
            val game = snapshot.game
            val hunterId = game.hunterUserId
            fun same(a: String?, b: String?) = a != null && b != null && a.equals(b, ignoreCase = true)
            val participants = game.participants.map { p ->
                GameParticipant(
                    userId = p.userId,
                    callsign = p.displayName,
                    state = p.state,
                    location = p.lastKnownLocation?.let { GeoPosition(it.latitude, it.longitude) },
                    isHunter = same(p.userId, hunterId),
                    hasActivePenalty = p.hasActivePenalty,
                    penaltyEndsAt = p.penaltyEndsAt,
                    isMe = same(p.userId, myUserId),
                )
            }
            val me = participants.firstOrNull { it.isMe }
            return ActiveGameState(
                gameId = game.id,
                role = when {
                    me == null -> GameRole.None
                    me.isHunter -> GameRole.Hunter
                    else -> GameRole.Prey
                },
                phase = if (game.status == GameStatuses.IN_PROGRESS) GamePhase.Playing else GamePhase.Starting,
                area = snapshot.playfieldCoordinates.map { GeoPosition(it.latitude, it.longitude) },
                hunterMayMoveAt = snapshot.status?.hunterMayMoveAt,
                serverTime = snapshot.serverTime,
                participants = participants,
            )
        }
    }
}
