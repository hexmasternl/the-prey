package nl.hexmaster.theprey.data.version

import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.sync.Mutex
import nl.hexmaster.theprey.auth.SessionState
import nl.hexmaster.theprey.data.game.ActiveGameResult
import nl.hexmaster.theprey.data.game.GameRepository
import java.io.IOException

/** What the update gate lets the player do. */
enum class GateState {
    /** The app is usable: up to date, or the check could not tell (fail open). */
    Open,

    /** An update is required, but the player is in a game that has not ended; block once it has. */
    Deferred,

    /** An update is required: only the Play Store link and leaving the app remain. */
    Blocked,
}

/** The server's answer to the version check. */
enum class VersionCheck {
    /** 204: the version is supported. */
    UpToDate,

    /** 409: the app must be updated. */
    UpdateRequired,

    /** Any other status, a network error or an unreadable answer. */
    Unknown,
}

/**
 * The gate for [version]. Only [VersionCheck.UpdateRequired] closes it; a player in a game
 * ([activeGame] `Active`) or whose game cannot be determined (`Failed`) is deferred, never blocked
 * on a blip. [activeGame] is only consulted when [signedIn].
 */
fun decide(version: VersionCheck, signedIn: Boolean, activeGame: ActiveGameResult?): GateState = when {
    version != VersionCheck.UpdateRequired -> GateState.Open
    !signedIn -> GateState.Blocked
    activeGame is ActiveGameResult.None -> GateState.Blocked
    else -> GateState.Deferred
}

/**
 * Asks the Games API whether [currentVersion] may still be used and exposes the outcome as [state].
 * Callers trigger [check]: on start, on every return to the foreground, every 10 minutes and when
 * the player leaves a lobby or game while [GateState.Deferred]. [debug] builds run the check but
 * never leave [GateState.Open]: their fallback `versionName` would always be refused.
 */
class AppVersionGate(
    private val api: VersionApi,
    private val currentVersion: String,
    private val debug: Boolean,
    private val session: StateFlow<SessionState>,
    private val games: GameRepository,
) {
    private val _state = MutableStateFlow(GateState.Open)
    val state: StateFlow<GateState> = _state.asStateFlow()

    private val running = Mutex()

    /** Runs one check; a call while another is running is dropped. */
    suspend fun check() {
        if (!running.tryLock()) return
        try {
            val version = askServer()
            var signedIn = false
            var activeGame: ActiveGameResult? = null
            if (version == VersionCheck.UpdateRequired) {
                // At app start the stored session is still being restored; wait for its answer.
                signedIn = session.first { it !is SessionState.Unknown } is SessionState.SignedIn
                if (signedIn) activeGame = games.activeGame()
            }
            val decided = decide(version, signedIn, activeGame)
            _state.value = if (debug) GateState.Open else decided
        } finally {
            running.unlock()
        }
    }

    private suspend fun askServer(): VersionCheck = try {
        val response = api.check(VersionCheckRequest(currentVersion))
        when (response.code()) {
            HTTP_NO_CONTENT -> VersionCheck.UpToDate
            HTTP_CONFLICT -> VersionCheck.UpdateRequired
            else -> VersionCheck.Unknown
        }
    } catch (e: CancellationException) {
        throw e
    } catch (_: IOException) {
        VersionCheck.Unknown
    } catch (_: RuntimeException) {
        VersionCheck.Unknown
    }

    private companion object {
        const val HTTP_NO_CONTENT = 204
        const val HTTP_CONFLICT = 409
    }
}
