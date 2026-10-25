package nl.hexmaster.theprey.data.hints

import android.util.Log
import androidx.datastore.core.DataStore
import androidx.datastore.preferences.core.Preferences
import androidx.datastore.preferences.core.booleanPreferencesKey
import androidx.datastore.preferences.core.edit
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.catch
import kotlinx.coroutines.flow.distinctUntilChanged
import kotlinx.coroutines.flow.map
import java.io.IOException

/** The first-time tours, each with its own "seen" flag. */
enum class Hint { PlayFieldArea, PreyHud, HunterHud }

/**
 * Device-local record of which first-time tours were completed or skipped. Not tied to an account:
 * it survives sign-out and is reset from Settings, by clearing app data or by reinstalling.
 */
interface HintsStore {
    /** True once [hint]'s tour was completed or skipped. A failing read gives false. */
    fun seen(hint: Hint): Flow<Boolean>

    /** Marks [hint] seen. A failing write is logged and swallowed: the tour may show again later. */
    suspend fun markSeen(hint: Hint)

    /** Marks every tour not seen. Throws when storage fails, so Settings can say so. */
    suspend fun resetAll()
}

/** [HintsStore] backed by its own Preferences DataStore (app-private, excluded from backups). */
class DataStoreHintsStore(private val dataStore: DataStore<Preferences>) : HintsStore {

    override fun seen(hint: Hint): Flow<Boolean> = dataStore.data
        .map { it[key(hint)] ?: false }
        .catch { e ->
            Log.w(TAG, "Hints unreadable", e)
            emit(false)
        }
        .distinctUntilChanged()

    override suspend fun markSeen(hint: Hint) {
        try {
            dataStore.edit { it[key(hint)] = true }
        } catch (e: IOException) {
            Log.w(TAG, "Hint $hint not stored", e)
        }
    }

    override suspend fun resetAll() {
        dataStore.edit { prefs -> Hint.entries.forEach { prefs.remove(key(it)) } }
    }

    private fun key(hint: Hint) = booleanPreferencesKey("seen_${hint.name}")

    private companion object {
        const val TAG = "HintsStore"
    }
}
