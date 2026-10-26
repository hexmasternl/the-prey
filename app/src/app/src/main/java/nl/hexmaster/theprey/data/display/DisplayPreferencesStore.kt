package nl.hexmaster.theprey.data.display

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

/**
 * Device-local display options. Not tied to an account and never sent to the server:
 * it survives sign-out and is reset only by clearing app data or reinstalling.
 */
interface DisplayPreferencesStore {
    /** True when the game screen keeps the display awake. A failing read gives false. */
    val keepScreenOnInGame: Flow<Boolean>

    /** Stores the choice. A failing write is logged and swallowed: the flow keeps the stored value. */
    suspend fun setKeepScreenOnInGame(enabled: Boolean)
}

/** [DisplayPreferencesStore] backed by its own Preferences DataStore (app-private, excluded from backups). */
class DataStoreDisplayPreferencesStore(private val dataStore: DataStore<Preferences>) : DisplayPreferencesStore {

    override val keepScreenOnInGame: Flow<Boolean> = dataStore.data
        .map { it[KEEP_SCREEN_ON_IN_GAME] ?: false }
        .catch { e ->
            Log.w(TAG, "Display preferences unreadable", e)
            emit(false)
        }
        .distinctUntilChanged()

    override suspend fun setKeepScreenOnInGame(enabled: Boolean) {
        try {
            dataStore.edit { it[KEEP_SCREEN_ON_IN_GAME] = enabled }
        } catch (e: IOException) {
            Log.w(TAG, "Keep screen on not stored", e)
        }
    }

    private companion object {
        const val TAG = "DisplayPreferences"
        val KEEP_SCREEN_ON_IN_GAME = booleanPreferencesKey("keep_screen_on_in_game")
    }
}
