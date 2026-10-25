package nl.hexmaster.theprey.data.hints

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
import org.junit.Assert.fail
import org.junit.Test
import java.io.IOException

class HintsStoreTest {

    @Test
    fun seen_ShouldBeFalseForEveryHint_WhenNothingIsStored() = runTest {
        val sut = DataStoreHintsStore(InMemoryDataStore())

        Hint.entries.forEach { assertFalse(it.name, sut.seen(it).first()) }
    }

    @Test
    fun markSeen_ShouldLeaveTheOtherHintsUnseen_WhenOneIsMarked() = runTest {
        val dataStore = InMemoryDataStore()
        DataStoreHintsStore(dataStore).markSeen(Hint.PreyHud)

        // A new store over the same file, as after an app restart.
        val sut = DataStoreHintsStore(dataStore)
        assertTrue(sut.seen(Hint.PreyHud).first())
        assertFalse(sut.seen(Hint.HunterHud).first())
        assertFalse(sut.seen(Hint.PlayFieldArea).first())
    }

    @Test
    fun resetAll_ShouldMarkEveryHintUnseen_WhenAllWereSeen() = runTest {
        val sut = DataStoreHintsStore(InMemoryDataStore())
        Hint.entries.forEach { sut.markSeen(it) }

        sut.resetAll()

        Hint.entries.forEach { assertFalse(it.name, sut.seen(it).first()) }
    }

    @Test
    fun seen_ShouldBeFalse_WhenTheReadFails() = runTest {
        val sut = DataStoreHintsStore(FailingDataStore())

        assertFalse(sut.seen(Hint.PlayFieldArea).first())
    }

    @Test
    fun markSeen_ShouldNotThrow_WhenTheWriteFails() = runTest {
        DataStoreHintsStore(FailingDataStore()).markSeen(Hint.HunterHud)
    }

    @Test
    fun resetAll_ShouldThrow_WhenTheWriteFails() = runTest {
        try {
            DataStoreHintsStore(FailingDataStore()).resetAll()
            fail("Expected the failure to propagate")
        } catch (_: IOException) {
        }
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
