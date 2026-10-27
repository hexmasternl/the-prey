package nl.hexmaster.theprey.ui.playfields

import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableSharedFlow
import nl.hexmaster.theprey.data.location.GeoPosition
import nl.hexmaster.theprey.data.location.LocationSource
import nl.hexmaster.theprey.data.playfield.GpsCoordinateDto
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

    // ---- Near me ----

    /** A location source the test feeds; [requests] counts collections. */
    private class FakeLocation : LocationSource {
        val fixes = MutableSharedFlow<GeoPosition>()
        var requests = 0
        override fun positions(): Flow<GeoPosition> {
            requests++
            return fixes
        }
    }

    private val location = FakeLocation()
    private val fix = GeoPosition(52.35812, 4.86871)

    private fun TestScope.nearMeSut(permission: Boolean = true, initialQuery: String = "", initialNearMe: Boolean = false) =
        PublicPlayFieldSearch(
            repository,
            backgroundScope.plusUnconfined(this),
            initialQuery,
            locationSource = location,
            hasLocationAccess = { permission },
            initialNearMe = initialNearMe,
        )

    /** A public playfield whose centre is [kmNorth] km north of [fix]; the server's distance is deliberately off. */
    private fun north(id: String, kmNorth: Double) =
        playFieldDto(id, isPublic = true).copy(
            centerCoordinates = GpsCoordinateDto(fix.latitude + kmNorth / 111.195, fix.longitude),
            distanceMeters = 99_999,
        )

    private suspend fun TestScope.switchOnWithFix(sut: PublicPlayFieldSearch) {
        sut.toggleNearMe()
        runCurrent()
        location.fixes.emit(fix)
        // The search starts once the (debounced) search text has been read once.
        advanceTimeBy(SEARCH_DEBOUNCE_MS + 1)
    }

    @Test
    fun toggleNearMe_ShouldSearchNearestFirstWithDeviceDistances_WhenFixArrives() = runTest {
        val sut = nearMeSut()

        switchOnWithFix(sut)

        assertEquals(NearMeState.On(fix), sut.nearMe.value)
        assertEquals(listOf(""), repository.searches)
        assertEquals(listOf<GeoPosition?>(fix), repository.origins)
        assertEquals(SearchState.Searching, sut.state.value)

        repository.searchCalls.single().complete(Result.success(listOf(north("far", 8.0), north("near", 1.2))))
        runCurrent()

        val items = (sut.state.value as SearchState.Results).items
        assertEquals(listOf("near", "far"), items.map { it.id })
        // Computed on the device from the unrounded fix, not the server's distance.
        assertEquals(1_200.0, items[0].distanceMeters!!.toDouble(), 5.0)
        assertEquals(8_000.0, items[1].distanceMeters!!.toDouble(), 5.0)
    }

    @Test
    fun toggleNearMe_ShouldFilterByName_WhenThreeOrMoreCharactersAreTyped() = runTest {
        val sut = nearMeSut()
        switchOnWithFix(sut)

        sut.onQueryChange("park")
        advanceTimeBy(SEARCH_DEBOUNCE_MS + 1)

        assertEquals(listOf("", "park"), repository.searches)
        assertEquals(fix, repository.origins.last())
    }

    @Test
    fun onQueryChange_ShouldListNearbyWithoutNameFilter_WhenTextIsShortAndNearMeIsOn() = runTest {
        val sut = nearMeSut(initialQuery = "park")
        advanceTimeBy(SEARCH_DEBOUNCE_MS + 1)
        switchOnWithFix(sut)

        sut.onQueryChange("pa")
        advanceTimeBy(SEARCH_DEBOUNCE_MS + 1)

        assertEquals(listOf("park", "park", ""), repository.searches)
        assertEquals(listOf(null, fix, fix), repository.origins)
        assertEquals(SearchState.Searching, sut.state.value)
    }

    @Test
    fun toggleNearMe_ShouldClearAndShowHint_WhenSwitchedOffWithShortText() = runTest {
        val sut = nearMeSut()
        switchOnWithFix(sut)
        repository.searchCalls.single().complete(Result.success(listOf(north("near", 1.0))))
        runCurrent()
        sut.onQueryChange("vo")
        advanceTimeBy(SEARCH_DEBOUNCE_MS + 1)
        val sent = repository.searches.size

        sut.toggleNearMe()
        advanceTimeBy(SEARCH_DEBOUNCE_MS * 3)

        assertEquals(NearMeState.Off, sut.nearMe.value)
        assertEquals(SearchState.Idle, sut.state.value)
        assertEquals(sent, repository.searches.size)
    }

    @Test
    fun toggleNearMe_ShouldSearchByNameOnly_WhenSwitchedOffWithLongText() = runTest {
        val sut = nearMeSut()
        switchOnWithFix(sut)
        sut.onQueryChange("park")
        advanceTimeBy(SEARCH_DEBOUNCE_MS + 1)

        sut.toggleNearMe()
        runCurrent()

        assertEquals("park", repository.searches.last())
        assertEquals(null, repository.origins.last())
    }

    @Test
    fun toggleNearMe_ShouldStayOffWithoutAskingForLocation_WhenPermissionIsMissing() = runTest {
        val sut = nearMeSut(permission = false)

        sut.toggleNearMe()
        runCurrent()

        assertEquals(NearMeState.NoPermission, sut.nearMe.value)
        assertEquals(0, location.requests)
        // Name search keeps working.
        sut.onQueryChange("park")
        advanceTimeBy(SEARCH_DEBOUNCE_MS + 1)
        assertEquals(listOf("park"), repository.searches)
        assertEquals(listOf<GeoPosition?>(null), repository.origins)
    }

    @Test
    fun toggleNearMe_ShouldStayOff_WhenNoFixArrivesWithinTenSeconds() = runTest {
        val sut = nearMeSut()

        sut.toggleNearMe()
        runCurrent()
        assertEquals(NearMeState.Locating, sut.nearMe.value)
        advanceTimeBy(NEAR_ME_FIX_TIMEOUT_MS + 1)

        assertEquals(NearMeState.NoFix, sut.nearMe.value)
        assertTrue(repository.searches.isEmpty())
    }

    @Test
    fun toggleNearMe_ShouldShowEmptyResults_WhenNothingIsNearby() = runTest {
        val sut = nearMeSut()
        switchOnWithFix(sut)

        repository.searchCalls.single().complete(Result.success(emptyList()))
        runCurrent()

        assertEquals(SearchState.Results(emptyList()), sut.state.value)
    }

    @Test
    fun init_ShouldLocateAgain_WhenNearMeWasRestored() = runTest {
        val sut = nearMeSut(initialNearMe = true)
        runCurrent()
        assertEquals(NearMeState.Locating, sut.nearMe.value)

        location.fixes.emit(fix)
        advanceTimeBy(SEARCH_DEBOUNCE_MS + 1)

        assertEquals(NearMeState.On(fix), sut.nearMe.value)
        assertEquals(listOf<GeoPosition?>(fix), repository.origins)
    }

    @Test
    fun search_ShouldSendNoPosition_WhenNearMeIsOff() = runTest {
        val sut = nearMeSut()

        sut.onQueryChange("vondel")
        advanceTimeBy(SEARCH_DEBOUNCE_MS + 1)

        assertEquals(listOf<GeoPosition?>(null), repository.origins)
        assertEquals(0, location.requests)
    }
}
