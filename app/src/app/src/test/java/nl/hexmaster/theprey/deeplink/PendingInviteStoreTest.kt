package nl.hexmaster.theprey.deeplink

import androidx.datastore.core.DataStore
import androidx.datastore.preferences.core.Preferences
import androidx.datastore.preferences.core.emptyPreferences
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

class PendingInviteStoreTest {

    private val first = JoinInvite("3f2b8c1e-0d4a-4c1b-9a7e-2b6f1c9d8e01", "4291")
    private val second = JoinInvite("7a1d2c3b-0000-4000-8000-000000000002", "1234")

    @Test
    fun pending_ShouldBeFalse_WhenNothingIsStored() = runTest {
        assertFalse(DataStoreInviteStore(InMemoryDataStore()).pending.first())
    }

    @Test
    fun take_ShouldReturnTheNewerInvite_WhenTwoWereSaved() = runTest {
        val sut = DataStoreInviteStore(InMemoryDataStore())

        sut.save(first, receivedAtMillis = 1_000)
        sut.save(second, receivedAtMillis = 2_000)

        assertTrue(sut.pending.first())
        assertEquals(second, sut.take(nowMillis = 3_000))
    }

    @Test
    fun take_ShouldReturnTheInviteOnce_WhenTakenTwice() = runTest {
        val dataStore = InMemoryDataStore()
        DataStoreInviteStore(dataStore).save(first, receivedAtMillis = 1_000)

        // A new store over the same file, as after the sign-in round trip.
        val sut = DataStoreInviteStore(dataStore)
        assertEquals(first, sut.take(nowMillis = 2_000))
        assertNull(sut.take(nowMillis = 2_000))
        assertFalse(sut.pending.first())
    }

    @Test
    fun take_ShouldReturnTheInvite_WhenExactlyAnHourOld() = runTest {
        val sut = DataStoreInviteStore(InMemoryDataStore())
        sut.save(first, receivedAtMillis = 0)

        assertEquals(first, sut.take(nowMillis = PendingInviteStore.EXPIRY_MILLIS))
    }

    @Test
    fun take_ShouldReturnNullAndClear_WhenOlderThanAnHour() = runTest {
        val sut = DataStoreInviteStore(InMemoryDataStore())
        sut.save(first, receivedAtMillis = 0)

        assertNull(sut.take(nowMillis = PendingInviteStore.EXPIRY_MILLIS + 1))
        assertFalse(sut.pending.first())
    }

    private class InMemoryDataStore : DataStore<Preferences> {
        private val current = MutableStateFlow(emptyPreferences())
        override val data: Flow<Preferences> = current

        override suspend fun updateData(transform: suspend (t: Preferences) -> Preferences): Preferences =
            transform(current.value).also { current.value = it }
    }
}
