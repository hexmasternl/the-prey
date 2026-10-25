package nl.hexmaster.theprey.ui.playfields.select

import androidx.lifecycle.SavedStateHandle
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.launch
import kotlinx.coroutines.test.UnconfinedTestDispatcher
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

@OptIn(ExperimentalCoroutinesApi::class)
class PlayFieldSelectResultTest {

    private val handle = SavedStateHandle()
    private val selection = PlayFieldSelection("pf-1", "Vondelpark", isPublic = true)

    @Test
    fun consume_ShouldEmitDeliveredSelection_WhenDelivered() = runTest {
        val received = mutableListOf<PlayFieldSelection>()
        backgroundScope.launch(UnconfinedTestDispatcher(testScheduler)) {
            PlayFieldSelectResult.consume(handle).collect { received += it }
        }

        PlayFieldSelectResult.deliver(handle, selection)

        assertEquals(listOf(selection), received)
    }

    @Test
    fun consume_ShouldEmitOnceAndRemoveKey_WhenCollectedTwice() = runTest {
        PlayFieldSelectResult.deliver(handle, selection)
        val first = mutableListOf<PlayFieldSelection>()
        val job = launch(UnconfinedTestDispatcher(testScheduler)) {
            PlayFieldSelectResult.consume(handle).collect { first += it }
        }
        job.cancel()

        val second = mutableListOf<PlayFieldSelection>()
        backgroundScope.launch(UnconfinedTestDispatcher(testScheduler)) {
            PlayFieldSelectResult.consume(handle).collect { second += it }
        }

        assertEquals(listOf(selection), first)
        assertTrue(second.isEmpty())
        assertNull(handle.get<String>(PlayFieldSelectResult.KEY))
    }

    @Test
    fun consume_ShouldEmitNothing_WhenNothingDelivered() = runTest {
        val received = mutableListOf<PlayFieldSelection>()
        backgroundScope.launch(UnconfinedTestDispatcher(testScheduler)) {
            PlayFieldSelectResult.consume(handle).collect { received += it }
        }

        assertTrue(received.isEmpty())
    }
}
