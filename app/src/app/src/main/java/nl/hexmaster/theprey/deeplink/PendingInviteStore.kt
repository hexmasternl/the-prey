package nl.hexmaster.theprey.deeplink

import androidx.datastore.core.DataStore
import androidx.datastore.preferences.core.Preferences
import androidx.datastore.preferences.core.edit
import androidx.datastore.preferences.core.longPreferencesKey
import androidx.datastore.preferences.core.stringPreferencesKey
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.distinctUntilChanged
import kotlinx.coroutines.flow.map

/**
 * The invitation the app received last and has not handled yet. Persisted, because signing in
 * leaves the app for the browser and the process may die in between. Not cleared on sign-out: the
 * invitation belongs to whoever signs in next.
 */
interface PendingInviteStore {
    /** True while an invitation is stored (expired or not; [take] decides). */
    val pending: Flow<Boolean>

    /** Stores [invite], received at [receivedAtMillis] (wall clock); replaces an older one. */
    suspend fun save(invite: JoinInvite, receivedAtMillis: Long)

    /** Reads and clears the invitation in one step, so it is used once. Null when none, or older than [EXPIRY_MILLIS]. */
    suspend fun take(nowMillis: Long): JoinInvite?

    companion object {
        const val EXPIRY_MILLIS = 60 * 60 * 1000L
    }
}

/** [PendingInviteStore] backed by its own Preferences DataStore (app-private, excluded from backups). */
class DataStoreInviteStore(private val dataStore: DataStore<Preferences>) : PendingInviteStore {

    override val pending: Flow<Boolean> = dataStore.data.map { it[GAME_ID_KEY] != null }.distinctUntilChanged()

    override suspend fun save(invite: JoinInvite, receivedAtMillis: Long) {
        dataStore.edit {
            it[GAME_ID_KEY] = invite.gameId
            it[CODE_KEY] = invite.code
            it[RECEIVED_AT_KEY] = receivedAtMillis
        }
    }

    override suspend fun take(nowMillis: Long): JoinInvite? {
        var taken: JoinInvite? = null
        dataStore.edit {
            val gameId = it[GAME_ID_KEY]
            val code = it[CODE_KEY]
            val receivedAt = it[RECEIVED_AT_KEY]
            if (gameId != null && code != null && receivedAt != null &&
                nowMillis - receivedAt <= PendingInviteStore.EXPIRY_MILLIS
            ) {
                taken = JoinInvite(gameId, code)
            }
            it.remove(GAME_ID_KEY)
            it.remove(CODE_KEY)
            it.remove(RECEIVED_AT_KEY)
        }
        return taken
    }

    private companion object {
        val GAME_ID_KEY = stringPreferencesKey("game_id")
        val CODE_KEY = stringPreferencesKey("code")
        val RECEIVED_AT_KEY = longPreferencesKey("received_at")
    }
}
