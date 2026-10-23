package nl.hexmaster.theprey.data.user

import androidx.datastore.core.DataStore
import androidx.datastore.preferences.core.Preferences
import androidx.datastore.preferences.core.edit
import androidx.datastore.preferences.core.stringPreferencesKey
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.flow.map
import kotlinx.serialization.SerializationException
import kotlinx.serialization.json.Json

/** Device cache of the signed-in player's profile, so a restored session needs no network. */
interface ProfileStore {
    val profile: Flow<UserDto?>
    suspend fun read(): UserDto? = profile.first()
    suspend fun save(profile: UserDto)
    suspend fun clear()
}

/** [ProfileStore] backed by a Preferences DataStore (app-private, excluded from backups). */
class DataStoreProfileStore(
    private val dataStore: DataStore<Preferences>,
    private val json: Json = Json { ignoreUnknownKeys = true },
) : ProfileStore {

    override val profile: Flow<UserDto?> = dataStore.data.map { preferences ->
        preferences[PROFILE_KEY]?.let { encoded ->
            try {
                json.decodeFromString(UserDto.serializer(), encoded)
            } catch (_: SerializationException) {
                null
            } catch (_: IllegalArgumentException) {
                null
            }
        }
    }

    override suspend fun save(profile: UserDto) {
        dataStore.edit { it[PROFILE_KEY] = json.encodeToString(UserDto.serializer(), profile) }
    }

    override suspend fun clear() {
        dataStore.edit { it.remove(PROFILE_KEY) }
    }

    private companion object {
        val PROFILE_KEY = stringPreferencesKey("profile")
    }
}
