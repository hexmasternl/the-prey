package nl.hexmaster.theprey.ui.components.tour

import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.flow
import kotlinx.coroutines.flow.map
import kotlinx.coroutines.test.UnconfinedTestDispatcher
import kotlinx.coroutines.test.runTest
import nl.hexmaster.theprey.data.hints.Hint
import nl.hexmaster.theprey.data.hints.HintsStore
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test
import java.io.IOException

@OptIn(ExperimentalCoroutinesApi::class)
class HintGateTest {

    private class FakeHintsStore(
        seen: Set<Hint> = emptySet(),
        private val failReads: Boolean = false,
        private val failWrites: Boolean = false,
    ) : HintsStore {
        private val current = MutableStateFlow(seen)
        val marked = mutableListOf<Hint>()

        override fun seen(hint: Hint): Flow<Boolean> =
            if (failReads) flow { throw IOException("disk") } else current.map { hint in it }

        override suspend fun markSeen(hint: Hint) {
            marked += hint
            if (failWrites) throw IOException("disk")
            current.value = current.value + hint
        }

        override suspend fun resetAll() {
            current.value = emptySet()
        }
    }

    @Test
    fun shows_ShouldBeFalse_WhenTheScreenIsNotReady() = runTest {
        val sut = HintGate(FakeHintsStore(), Hint.PreyHud, backgroundScope.unconfined())

        assertFalse(sut.shows(ready = false))
        assertTrue(sut.shows(ready = true))
    }

    @Test
    fun shows_ShouldBeFalse_WhenTheTourWasSeen() = runTest {
        val sut = HintGate(FakeHintsStore(seen = setOf(Hint.PreyHud)), Hint.PreyHud, backgroundScope.unconfined())

        assertFalse(sut.shows(ready = true))
    }

    @Test
    fun shows_ShouldBeTrue_WhenTheFlagCannotBeRead() = runTest {
        val sut = HintGate(FakeHintsStore(failReads = true), Hint.HunterHud, backgroundScope.unconfined())

        assertTrue(sut.shows(ready = true))
    }

    @Test
    fun finish_ShouldHideAndMarkSeen_WhenTheTourCloses() = runTest {
        val store = FakeHintsStore()
        val sut = HintGate(store, Hint.PlayFieldArea, backgroundScope.unconfined())

        sut.finish()

        assertFalse(sut.shows(ready = true))
        assertEquals(listOf(Hint.PlayFieldArea), store.marked)
    }

    @Test
    fun finish_ShouldHide_WhenTheWriteFails() = runTest {
        val store = FakeHintsStore(failWrites = true)
        val sut = HintGate(store, Hint.PlayFieldArea, backgroundScope.unconfined())

        sut.finish()

        assertFalse(sut.shows(ready = true))
        assertEquals(listOf(Hint.PlayFieldArea), store.marked)
    }

    private fun kotlinx.coroutines.CoroutineScope.unconfined() =
        kotlinx.coroutines.CoroutineScope(coroutineContext + UnconfinedTestDispatcher())
}
