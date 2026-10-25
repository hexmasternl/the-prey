package nl.hexmaster.theprey.ui.playfields

import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.test.TestScope
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Test
import java.io.IOException

@OptIn(ExperimentalCoroutinesApi::class)
class OwnPlayFieldsTest {

    private val repository = ScriptedPlayFieldRepository()

    private fun TestScope.sut() = OwnPlayFields(repository, backgroundScope.plusUnconfined(this))

    private fun OwnPlayFields.ids() = (state.value as ListState.Loaded).items.map { it.id }

    @Test
    fun refresh_ShouldShowLoadingThenLoaded_WhenFirstLoadSucceeds() = runTest {
        repository.mineAnswer = null
        val sut = sut()

        sut.refresh()
        assertEquals(ListState.Loading, sut.state.value)

        repository.mineCalls.single().complete(Result.success(listOf(playFieldDto("a"), playFieldDto("b"))))
        assertEquals(listOf("a", "b"), sut.ids())
    }

    @Test
    fun refresh_ShouldShowError_WhenFirstLoadFails() = runTest {
        repository.mineAnswer = { Result.failure(IOException("offline")) }
        val sut = sut()

        sut.refresh()

        assertEquals(ListState.Error, sut.state.value)
    }

    @Test
    fun refresh_ShouldShowLoadingAgain_WhenRetriedAfterError() = runTest {
        repository.mineAnswer = { Result.failure(IOException("offline")) }
        val sut = sut()
        sut.refresh()

        repository.mineAnswer = null
        sut.refresh()
        assertEquals(ListState.Loading, sut.state.value)

        repository.mineCalls.single().complete(Result.success(listOf(playFieldDto("a"))))
        assertEquals(listOf("a"), sut.ids())
    }

    @Test
    fun refresh_ShouldKeepItemsVisible_WhenRefreshing() = runTest {
        repository.mineAnswer = { Result.success(listOf(playFieldDto("a"))) }
        val sut = sut()
        sut.refresh()

        repository.mineAnswer = null
        sut.refresh()
        assertEquals(listOf("a"), sut.ids())

        repository.mineCalls.single().complete(Result.success(listOf(playFieldDto("a"), playFieldDto("b"))))
        assertEquals(listOf("a", "b"), sut.ids())
    }

    @Test
    fun refresh_ShouldKeepItems_WhenRefreshFails() = runTest {
        repository.mineAnswer = { Result.success(listOf(playFieldDto("a"))) }
        val sut = sut()
        sut.refresh()

        repository.mineAnswer = { Result.failure(IOException("offline")) }
        sut.refresh()

        assertEquals(listOf("a"), sut.ids())
    }

    @Test
    fun refresh_ShouldSendOneRequest_WhenALoadIsInFlight() = runTest {
        repository.mineAnswer = null
        val sut = sut()

        sut.refresh()
        sut.refresh()

        assertEquals(1, repository.mineCount)
    }

    @Test
    fun remove_ShouldDropTheItem_WhenListIsLoaded() = runTest {
        repository.mineAnswer = { Result.success(listOf(playFieldDto("a"), playFieldDto("b"))) }
        val sut = sut()
        sut.refresh()

        sut.remove("a")

        assertEquals(listOf("b"), sut.ids())
    }

    @Test
    fun remove_ShouldKeepState_WhenNothingIsLoaded() = runTest {
        repository.mineAnswer = { Result.failure(IOException("offline")) }
        val sut = sut()
        sut.refresh()

        sut.remove("a")

        assertEquals(ListState.Error, sut.state.value)
    }
}
