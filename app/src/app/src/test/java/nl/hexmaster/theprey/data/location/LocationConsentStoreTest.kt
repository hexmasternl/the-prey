package nl.hexmaster.theprey.data.location

import androidx.datastore.core.DataStore
import androidx.datastore.preferences.core.Preferences
import androidx.datastore.preferences.core.emptyPreferences
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class LocationConsentStoreTest {

    @Test
    fun isDisclosureAccepted_ShouldBeFalse_WhenNothingIsStored() = runTest {
        val sut = DataStoreLocationConsentStore(InMemoryDataStore())

        assertFalse(sut.isDisclosureAccepted())
    }

    @Test
    fun isDisclosureAccepted_ShouldPersist_WhenAcceptanceIsRecorded() = runTest {
        val dataStore = InMemoryDataStore()
        DataStoreLocationConsentStore(dataStore).setDisclosureAccepted(true)

        // A new store over the same file, as after an app restart.
        assertTrue(DataStoreLocationConsentStore(dataStore).isDisclosureAccepted())
    }

    @Test
    fun isDisclosureAccepted_ShouldBeFalse_WhenResetAfterAcceptance() = runTest {
        val sut = DataStoreLocationConsentStore(InMemoryDataStore())
        sut.setDisclosureAccepted(true)

        sut.setDisclosureAccepted(false)

        assertFalse(sut.isDisclosureAccepted())
    }

    @Test
    fun isMenuPromptShown_ShouldBeFalse_WhenNothingIsStored() = runTest {
        assertFalse(DataStoreLocationConsentStore(InMemoryDataStore()).isMenuPromptShown())
    }

    @Test
    fun isMenuPromptShown_ShouldPersist_WhenRecorded() = runTest {
        val dataStore = InMemoryDataStore()
        DataStoreLocationConsentStore(dataStore).setMenuPromptShown()

        val restarted = DataStoreLocationConsentStore(dataStore)
        assertTrue(restarted.isMenuPromptShown())
        assertFalse("Independent of the acceptance", restarted.isDisclosureAccepted())
    }

    /** In-memory DataStore: the file-backed one cannot replace its file on Windows JVMs. */
    private class InMemoryDataStore : DataStore<Preferences> {
        private val current = MutableStateFlow(emptyPreferences())
        override val data: Flow<Preferences> = current

        override suspend fun updateData(transform: suspend (t: Preferences) -> Preferences): Preferences =
            transform(current.value).also { current.value = it }
    }
}
