package nl.hexmaster.theprey.data.display

import androidx.datastore.core.DataStore
import androidx.datastore.preferences.core.Preferences
import androidx.datastore.preferences.core.emptyPreferences
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.flow.flow
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test
import java.io.IOException

class DisplayPreferencesStoreTest {

    @Test
    fun keepScreenOnInGame_ShouldBeFalse_WhenNothingIsStored() = runTest {
        val sut = DataStoreDisplayPreferencesStore(InMemoryDataStore())

        assertFalse(sut.keepScreenOnInGame.first())
    }

    @Test
    fun setKeepScreenOnInGame_ShouldRoundTrip_WhenTurnedOnAndOff() = runTest {
        val dataStore = InMemoryDataStore()
        DataStoreDisplayPreferencesStore(dataStore).setKeepScreenOnInGame(true)

        // A new store over the same file, as after an app restart.
        val sut = DataStoreDisplayPreferencesStore(dataStore)
        assertTrue(sut.keepScreenOnInGame.first())

        sut.setKeepScreenOnInGame(false)
        assertFalse(sut.keepScreenOnInGame.first())
    }

    @Test
    fun keepScreenOnInGame_ShouldBeFalse_WhenTheReadFails() = runTest {
        val sut = DataStoreDisplayPreferencesStore(FailingDataStore())

        assertFalse(sut.keepScreenOnInGame.first())
    }

    @Test
    fun setKeepScreenOnInGame_ShouldNotThrow_WhenTheWriteFails() = runTest {
        DataStoreDisplayPreferencesStore(FailingDataStore()).setKeepScreenOnInGame(true)
    }

    private class InMemoryDataStore : DataStore<Preferences> {
        private val current = MutableStateFlow(emptyPreferences())
        override val data: Flow<Preferences> = current

        override suspend fun updateData(transform: suspend (t: Preferences) -> Preferences): Preferences =
            transform(current.value).also { current.value = it }
    }

    private class FailingDataStore : DataStore<Preferences> {
        override val data: Flow<Preferences> = flow { throw IOException("disk") }

        override suspend fun updateData(transform: suspend (t: Preferences) -> Preferences): Preferences =
            throw IOException("disk")
    }
}
