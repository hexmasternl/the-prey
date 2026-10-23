package nl.hexmaster.theprey.data.user

import androidx.datastore.core.DataStore
import androidx.datastore.preferences.core.Preferences
import androidx.datastore.preferences.core.edit
import androidx.datastore.preferences.core.emptyPreferences
import androidx.datastore.preferences.core.stringPreferencesKey
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.test.runTest
import kotlinx.serialization.json.Json
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test

class UserDataTest {

    private val json = Json { ignoreUnknownKeys = true }

    private val profile = UserDto(
        userId = "6f1c1d0e-8f43-4a8e-9a43-3d8b0e1f2a11",
        displayName = "Nina Hawk",
        callsign = "Night-Hawk_7",
        emailAddress = "nina@example.com",
        preferredLanguage = "nl",
    )

    @Test
    fun userDto_ShouldDeserialize_WhenPayloadComesFromUsersApi() {
        val payload = """
            {"userId":"6f1c1d0e-8f43-4a8e-9a43-3d8b0e1f2a11","displayName":"Nina Hawk",
             "callsign":"Night-Hawk_7","emailAddress":"nina@example.com","preferredLanguage":"nl",
             "somethingNew":true}
        """.trimIndent()

        assertEquals(profile, json.decodeFromString(UserDto.serializer(), payload))
    }

    @Test
    fun createUserRequest_ShouldSerializeCamelCaseFields_WhenSentToUsersApi() {
        val request = CreateUserRequest("Nina", "Hawk", "nina@example.com", true, "en")

        val encoded = json.encodeToString(CreateUserRequest.serializer(), request)

        assertEquals(
            """{"firstName":"Nina","lastName":"Hawk","emailAddress":"nina@example.com","isEmailVerified":true,"preferredLanguage":"en"}""",
            encoded,
        )
    }

    @Test
    fun profileStore_ShouldRoundTripAndClear_WhenProfileIsSaved() = runTest {
        val sut = DataStoreProfileStore(InMemoryDataStore(), json)

        assertNull(sut.read())

        sut.save(profile)
        assertEquals(profile, sut.read())

        sut.clear()
        assertNull(sut.read())
    }

    @Test
    fun profileStore_ShouldReturnNull_WhenStoredProfileIsCorrupt() = runTest {
        val dataStore = InMemoryDataStore()
        dataStore.edit { it[stringPreferencesKey("profile")] = "{not json" }

        assertNull(DataStoreProfileStore(dataStore, json).read())
    }

    /**
     * In-memory DataStore: the file-backed one cannot replace its file on Windows JVMs
     * (it works on Android), and the mapping logic is what is under test here.
     */
    private class InMemoryDataStore : DataStore<Preferences> {
        private val current = MutableStateFlow(emptyPreferences())
        override val data: Flow<Preferences> = current

        override suspend fun updateData(transform: suspend (t: Preferences) -> Preferences): Preferences =
            transform(current.value).also { current.value = it }
    }
}
