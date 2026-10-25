package nl.hexmaster.theprey.ui.playfields.select

import androidx.lifecycle.SavedStateHandle
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.filterNotNull
import kotlinx.coroutines.flow.mapNotNull
import kotlinx.serialization.Serializable
import kotlinx.serialization.SerializationException
import kotlinx.serialization.json.Json

/** The playfield chosen on the playfield selection screen. */
@Serializable
data class PlayFieldSelection(val id: String, val name: String, val isPublic: Boolean)

/**
 * Hands a [PlayFieldSelection] back from the selection screen to the destination that opened it,
 * through that destination's back-stack [SavedStateHandle]. Stored as a JSON string, because a
 * saved state only holds bundle types. Cancel writes nothing.
 */
object PlayFieldSelectResult {
    const val KEY = "playfield_select_result"

    /** Writes [selection] for the caller whose back-stack entry owns [handle]. */
    fun deliver(handle: SavedStateHandle, selection: PlayFieldSelection) {
        handle[KEY] = Json.encodeToString(selection)
    }

    /** Emits a delivered selection once, then removes it so it is never applied twice. */
    fun consume(handle: SavedStateHandle): Flow<PlayFieldSelection> =
        handle.getStateFlow<String?>(KEY, null)
            .filterNotNull()
            .mapNotNull { take(handle) }

    /**
     * Reads and removes a delivered selection right now, or null when there is none. For callers
     * that must know the result synchronously, e.g. when their entry resumes after the picker.
     */
    fun take(handle: SavedStateHandle): PlayFieldSelection? {
        val json = handle.get<String>(KEY) ?: return null
        handle[KEY] = null
        return try {
            Json.decodeFromString<PlayFieldSelection>(json)
        } catch (_: SerializationException) {
            null
        } catch (_: IllegalArgumentException) {
            null
        }
    }
}
