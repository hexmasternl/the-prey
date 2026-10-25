package nl.hexmaster.theprey.tracking

import androidx.datastore.core.DataStore
import androidx.datastore.preferences.core.Preferences
import androidx.datastore.preferences.core.emptyPreferences
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

class TrackingSessionStoreTest {

    @Test
    fun read_ShouldBeNull_WhenNothingIsStored() = runTest {
        assertNull(DataStoreTrackingSessionStore(InMemoryDataStore()).read())
    }

    @Test
    fun read_ShouldReturnTheSession_WhenSavedBeforeARestart() = runTest {
        val dataStore = InMemoryDataStore()
        DataStoreTrackingSessionStore(dataStore).save(TrackingSession("game-1", 1_000L))

        // A new store over the same file, as after a process restart.
        assertEquals(TrackingSession("game-1", 1_000L, stopped = false), DataStoreTrackingSessionStore(dataStore).read())
    }

    @Test
    fun read_ShouldBeNull_WhenCleared() = runTest {
        val sut = DataStoreTrackingSessionStore(InMemoryDataStore())
        sut.save(TrackingSession("game-1", 1_000L))

        sut.clear()

        assertNull(sut.read())
    }

    @Test
    fun markStopped_ShouldSetStopped_WhenTheStoredGameMatches() = runTest {
        val sut = DataStoreTrackingSessionStore(InMemoryDataStore())
        sut.save(TrackingSession("game-1", 1_000L))

        sut.markStopped("GAME-1")

        assertTrue(sut.read()!!.stopped)
    }

    @Test
    fun markStopped_ShouldLeaveTheSession_WhenAnotherGameIsStored() = runTest {
        val sut = DataStoreTrackingSessionStore(InMemoryDataStore())
        sut.save(TrackingSession("game-2", 1_000L))

        sut.markStopped("game-1")

        assertFalse(sut.read()!!.stopped)
    }

    @Test
    fun bootAction_ShouldPromptResume_WhenSessionIsUnexpiredAndNotStopped() {
        assertEquals(BootAction.PromptResume("game-1"), bootAction(TrackingSession("game-1", 2_000L), nowMillis = 1_000L))
    }

    @Test
    fun bootAction_ShouldClear_WhenSessionIsExpired() {
        assertEquals(BootAction.Clear, bootAction(TrackingSession("game-1", 1_000L), nowMillis = 1_000L))
        assertEquals(BootAction.Clear, bootAction(TrackingSession("game-1", 1_000L, stopped = true), nowMillis = 5_000L))
    }

    @Test
    fun bootAction_ShouldDoNothing_WhenNothingIsStoredOrTrackingStopped() {
        assertEquals(BootAction.Nothing, bootAction(null, nowMillis = 1_000L))
        assertEquals(BootAction.Nothing, bootAction(TrackingSession("game-1", 2_000L, stopped = true), nowMillis = 1_000L))
    }

    /** In-memory DataStore: the file-backed one cannot replace its file on Windows JVMs. */
    private class InMemoryDataStore : DataStore<Preferences> {
        private val current = MutableStateFlow(emptyPreferences())
        override val data: Flow<Preferences> = current

        override suspend fun updateData(transform: suspend (t: Preferences) -> Preferences): Preferences =
            transform(current.value).also { current.value = it }
    }
}
