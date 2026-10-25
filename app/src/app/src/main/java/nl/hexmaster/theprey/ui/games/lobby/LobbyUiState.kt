package nl.hexmaster.theprey.ui.games.lobby

import nl.hexmaster.theprey.data.game.ConnectionState
import nl.hexmaster.theprey.data.game.FullGameStateDto
import nl.hexmaster.theprey.data.game.GameStatuses
import nl.hexmaster.theprey.ui.games.create.GameSettings
import nl.hexmaster.theprey.ui.games.create.toSettings

/** The lobby command in flight; while one runs, every lobby control is locked. */
enum class LobbyCommand { Setting, Hunter, Ready, Start, Leave }

/** One participant row. */
data class LobbyPlayerRow(
    val userId: String,
    val callsign: String,
    val isHunter: Boolean,
    /** The owner always shows as ready. */
    val isReady: Boolean,
    val isMe: Boolean,
)

/** Why Start game is still disabled, in priority order. */
enum class StartHint { WaitingForPlayers, ChooseHunter, PlayersGettingReady }

/** Everything the lobby screen shows, derived from the latest server state only. */
data class LobbyUiState(
    val gameCode: String,
    val isOwner: Boolean,
    val settings: GameSettings,
    val rows: List<LobbyPlayerRow>,
    /** Rows marked ready, the owner included. */
    val readyCount: Int,
    /** Owner only: the settings and the rows can be changed (nothing in flight). */
    val canEdit: Boolean,
    /** Non-owners only: Set ready is enabled. */
    val canSetReady: Boolean,
    /** Owner only: Start game is enabled. Follows the server's `Ready` status, never a local rule. */
    val canStart: Boolean,
    /** Owner only, while Start is disabled: what is still missing. */
    val startHint: StartHint?,
    val pending: LobbyCommand?,
    val reconnecting: Boolean,
) {
    companion object {
        private const val MIN_PLAYERS = 2

        /**
         * Maps [snapshot] for the player [myUserId]. Ownership comes from `ownerUserId`, never the
         * per-caller `isOwnerPlayer`, which group broadcasts do not refresh.
         */
        fun from(
            snapshot: FullGameStateDto,
            myUserId: String?,
            pending: LobbyCommand?,
            connection: ConnectionState = ConnectionState.Connected,
        ): LobbyUiState {
            val game = snapshot.game
            val owner = game.ownerUserId
            val isOwner = myUserId != null && owner.equals(myUserId, ignoreCase = true)
            val rows = game.participants.map { p ->
                LobbyPlayerRow(
                    userId = p.userId,
                    callsign = p.displayName,
                    isHunter = game.hunterUserId != null && game.hunterUserId.equals(p.userId, ignoreCase = true),
                    isReady = p.isReady || p.userId.equals(owner, ignoreCase = true),
                    isMe = myUserId != null && p.userId.equals(myUserId, ignoreCase = true),
                )
            }
            val idle = pending == null
            val ready = game.status == GameStatuses.READY
            val myRow = rows.firstOrNull { it.isMe }
            return LobbyUiState(
                gameCode = game.gameCode,
                isOwner = isOwner,
                settings = game.configuration.toSettings(),
                rows = rows,
                readyCount = rows.count { it.isReady },
                canEdit = isOwner && idle,
                canSetReady = !isOwner && idle && myRow != null && !myRow.isReady,
                canStart = isOwner && idle && ready,
                startHint = when {
                    !isOwner || ready -> null
                    rows.size < MIN_PLAYERS -> StartHint.WaitingForPlayers
                    rows.none { it.isHunter } -> StartHint.ChooseHunter
                    rows.any { !it.isReady } -> StartHint.PlayersGettingReady
                    else -> null
                },
                pending = pending,
                reconnecting = connection == ConnectionState.Reconnecting,
            )
        }
    }
}
