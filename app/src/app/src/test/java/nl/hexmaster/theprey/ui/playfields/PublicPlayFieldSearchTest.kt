package nl.hexmaster.theprey.ui.playfields

import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.test.TestScope
import kotlinx.coroutines.test.advanceTimeBy
import kotlinx.coroutines.test.runCurrent
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test
import java.io.IOException

@OptIn(ExperimentalCoroutinesApi::class)
class PublicPlayFieldSearchTest {

    private val repository = ScriptedPlayFieldRepository()

    private fun TestScope.sut(initialQuery: String = "") =
        PublicPlayFieldSearch(repository, backgroundScope.plusUnconfined(this), initialQuery)

    @Test
    fun onQueryChange_ShouldSearchOnceAfterDebounce_WhenTypingQuickly() = runTest {
        val sut = sut()

        sut.onQueryChange("von")
        advanceTimeBy(200)
        sut.onQueryChange("vondel")
        advanceTimeBy(SEARCH_DEBOUNCE_MS - 1)
        runCurrent()
        assertTrue(repository.searches.isEmpty())

        advanceTimeBy(2)
        runCurrent()

        assertEquals(listOf("vondel"), repository.searches)
        assertEquals(SearchState.Searching, sut.state.value)
    }

    @Test
    fun onQueryChange_ShouldClearAtOnceWithoutRequest_WhenQueryIsShort() = runTest {
        val sut = sut()
        sut.onQueryChange("vondel")
        advanceTimeBy(SEARCH_DEBOUNCE_MS + 1)
        repository.searchCalls.single().complete(Result.success(listOf(playFieldDto("p", isPublic = true))))
        runCurrent()
        assertTrue(sut.state.value is SearchState.Results)

        sut.onQueryChange("vo")

        assertEquals(SearchState.Idle, sut.state.value)
        assertEquals("vo", sut.query.value)
        advanceTimeBy(SEARCH_DEBOUNCE_MS * 3)
        assertEquals(listOf("vondel"), repository.searches)
    }

    @Test
    fun search_ShouldIgnoreStaleResult_WhenANewerSearchWasSent() = runTest {
        val sut = sut()

        sut.onQueryChange("vond")
        advanceTimeBy(SEARCH_DEBOUNCE_MS + 1)
        sut.onQueryChange("vondel")
        advanceTimeBy(SEARCH_DEBOUNCE_MS + 1)
        repository.searchCalls[1].complete(Result.success(listOf(playFieldDto("vondelpark", isPublic = true))))
        repository.searchCalls[0].complete(Result.success(listOf(playFieldDto("vondst", isPublic = true))))
        runCurrent()

        assertEquals(listOf("vondelpark"), (sut.state.value as SearchState.Results).items.map { it.id })
    }

    @Test
    fun search_ShouldSearchSameTextAgain_WhenPreviousSearchFailed() = runTest {
        val sut = sut()
        sut.onQueryChange("von")
        advanceTimeBy(SEARCH_DEBOUNCE_MS + 1)
        repository.searchCalls.single().complete(Result.failure(IOException("offline")))
        runCurrent()
        assertEquals(SearchState.Error, sut.state.value)

        sut.onQueryChange("von ")
        advanceTimeBy(SEARCH_DEBOUNCE_MS + 1)

        assertEquals(listOf("von", "von"), repository.searches)
    }

    @Test
    fun search_ShouldSearchSameTextAgain_WhenClearedInBetween() = runTest {
        val sut = sut()
        sut.onQueryChange("vondel")
        advanceTimeBy(SEARCH_DEBOUNCE_MS + 1)
        repository.searchCalls.single().complete(Result.success(emptyList()))
        runCurrent()

        sut.onQueryChange("vo")
        advanceTimeBy(100)
        sut.onQueryChange("vondel")
        advanceTimeBy(SEARCH_DEBOUNCE_MS + 1)

        assertEquals(listOf("vondel", "vondel"), repository.searches)
    }

    @Test
    fun init_ShouldSearchRestoredQuery_WhenInitialQueryIsLongEnough() = runTest {
        val sut = sut(initialQuery = "park")

        advanceTimeBy(SEARCH_DEBOUNCE_MS + 1)

        assertEquals("park", sut.query.value)
        assertEquals(listOf("park"), repository.searches)
    }
}
