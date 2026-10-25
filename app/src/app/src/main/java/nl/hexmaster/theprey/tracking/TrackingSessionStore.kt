package nl.hexmaster.theprey.tracking

import androidx.datastore.core.DataStore
import androidx.datastore.preferences.core.Preferences
import androidx.datastore.preferences.core.booleanPreferencesKey
import androidx.datastore.preferences.core.edit
import androidx.datastore.preferences.core.longPreferencesKey
import androidx.datastore.preferences.core.stringPreferencesKey
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.flow.map

/**
 * The game the location tracking runs (or ran) for. [deadlineMillis] is the device-clock moment
 * tracking must have stopped by; [stopped] blocks any automatic restart for this game.
 */
data class TrackingSession(
    val gameId: String,
    val deadlineMillis: Long,
    val stopped: Boolean = false,
) {
    fun isExpired(nowMillis: Long): Boolean = nowMillis >= deadlineMillis
}

/**
 * Persisted tracking session, so a system restart of the service and the boot receiver find the
 * game without the network. Written when tracking is validated, cleared on sign-out.
 */
interface TrackingSessionStore {
    val session: Flow<TrackingSession?>
    suspend fun read(): TrackingSession? = session.first()
    suspend fun save(session: TrackingSession)
    suspend fun clear()
}

/** Marks the stored session for [gameId] as stopped, so nothing restarts tracking for it. */
suspend fun TrackingSessionStore.markStopped(gameId: String) {
    val current = read() ?: return
    if (current.gameId.equals(gameId, ignoreCase = true) && !current.stopped) save(current.copy(stopped = true))
}

/** [TrackingSessionStore] backed by a Preferences DataStore (app-private, excluded from backups). */
class DataStoreTrackingSessionStore(private val dataStore: DataStore<Preferences>) : TrackingSessionStore {

    override val session: Flow<TrackingSession?> = dataStore.data.map { preferences ->
        val gameId = preferences[GAME_ID_KEY] ?: return@map null
        val deadline = preferences[DEADLINE_KEY] ?: return@map null
        TrackingSession(gameId, deadline, preferences[STOPPED_KEY] ?: false)
    }

    override suspend fun save(session: TrackingSession) {
        dataStore.edit {
            it[GAME_ID_KEY] = session.gameId
            it[DEADLINE_KEY] = session.deadlineMillis
            it[STOPPED_KEY] = session.stopped
        }
    }

    override suspend fun clear() {
        dataStore.edit {
            it.remove(GAME_ID_KEY)
            it.remove(DEADLINE_KEY)
            it.remove(STOPPED_KEY)
        }
    }

    private companion object {
        val GAME_ID_KEY = stringPreferencesKey("game_id")
        val DEADLINE_KEY = longPreferencesKey("deadline")
        val STOPPED_KEY = booleanPreferencesKey("stopped")
    }
}
