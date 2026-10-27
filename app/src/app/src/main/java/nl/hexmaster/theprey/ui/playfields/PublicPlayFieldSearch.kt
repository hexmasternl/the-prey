package nl.hexmaster.theprey.ui.playfields

import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.FlowPreview
import kotlinx.coroutines.Job
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.combine
import kotlinx.coroutines.flow.debounce
import kotlinx.coroutines.flow.distinctUntilChanged
import kotlinx.coroutines.flow.emptyFlow
import kotlinx.coroutines.flow.firstOrNull
import kotlinx.coroutines.flow.map
import kotlinx.coroutines.flow.transformLatest
import kotlinx.coroutines.launch
import kotlinx.coroutines.withTimeoutOrNull
import nl.hexmaster.theprey.data.location.GeoPosition
import nl.hexmaster.theprey.data.location.LocationSource
import nl.hexmaster.theprey.data.playfield.PlayFieldRepository

/** Quiet time after the last keystroke before the public search is sent. */
const val SEARCH_DEBOUNCE_MS = 366L

/** The server rejects shorter (trimmed) public searches. */
const val MIN_SEARCH_LENGTH = 3

/** How long Near me waits for a location fix before it gives up. */
const val NEAR_ME_FIX_TIMEOUT_MS = 10_000L

/** The public search (Public tab). */
sealed interface SearchState {
    /** Fewer than [MIN_SEARCH_LENGTH] characters: show the hint. */
    data object Idle : SearchState
    data object Searching : SearchState

    /** An empty list is the no-results state. */
    data class Results(val items: List<PlayFieldItem>) : SearchState
    data object Error : SearchState
}

/** The Near me option of the Public tab. */
sealed interface NearMeState {
    /** Name search only. */
    data object Off : NearMeState

    /** Waiting for one location fix. */
    data object Locating : NearMeState

    /** Searching around [position], the device's own (unrounded) fix. */
    data class On(val position: GeoPosition) : NearMeState

    /** Off: no location permission; Near me never asks for one. */
    data object NoPermission : NearMeState

    /** Off: no fix within [NEAR_ME_FIX_TIMEOUT_MS]. */
    data object NoFix : NearMeState
}

/** No location at all: the default when a screen offers no Near me. */
private object NoLocation : LocationSource {
    override fun positions() = emptyFlow<GeoPosition>()
}

/** Whether Near me is switched on (the chip is selected). */
val NearMeState.isOn: Boolean get() = this is NearMeState.On || this == NearMeState.Locating

/**
 * The debounced search over public playfields, by name and optionally near the player. Shared by
 * the playfields list and the playfield selection screen; [scope] is the owning view model's scope,
 * [initialQuery] a restored text and [initialNearMe] a restored Near me switch (it takes a new fix).
 *
 * Near me takes one fix from [locationSource] when switched on, never requests a permission
 * ([hasLocationAccess] is false without one), and searches the playfields within 25 km of it,
 * nearest first; with [MIN_SEARCH_LENGTH] or more characters it filters by name too.
 */
class PublicPlayFieldSearch(
    private val repository: PlayFieldRepository,
    private val scope: CoroutineScope,
    initialQuery: String = "",
    private val locationSource: LocationSource = NoLocation,
    private val hasLocationAccess: () -> Boolean = { false },
    initialNearMe: Boolean = false,
) {
    private val _query = MutableStateFlow(initialQuery)
    val query: StateFlow<String> = _query.asStateFlow()

    private val _state = MutableStateFlow<SearchState>(SearchState.Idle)
    val state: StateFlow<SearchState> = _state.asStateFlow()

    private val _nearMe = MutableStateFlow<NearMeState>(NearMeState.Off)
    val nearMe: StateFlow<NearMeState> = _nearMe.asStateFlow()

    /**
     * Bumped when the same trimmed text must be searched again: after a failed search, and after
     * the results were cleared below [MIN_SEARCH_LENGTH] (the debounce may never see the short text).
     */
    private var generation = 0

    private var locating: Job? = null

    init {
        scope.launch { search() }
        if (initialNearMe) toggleNearMe()
    }

    fun onQueryChange(text: String) {
        if (text.trim().length < MIN_SEARCH_LENGTH && _nearMe.value !is NearMeState.On) {
            // Cleared at once, not after the debounce.
            generation++
            _state.value = SearchState.Idle
        }
        _query.value = text
    }

    /** Switches Near me on (taking one fix) or off (back to name search). */
    fun toggleNearMe() {
        if (_nearMe.value.isOn) {
            locating?.cancel()
            generation++
            _nearMe.value = NearMeState.Off
            if (_query.value.trim().length < MIN_SEARCH_LENGTH) _state.value = SearchState.Idle
            return
        }
        if (!hasLocationAccess()) {
            _nearMe.value = NearMeState.NoPermission
            return
        }
        _nearMe.value = NearMeState.Locating
        locating = scope.launch {
            val fix = withTimeoutOrNull(NEAR_ME_FIX_TIMEOUT_MS) { locationSource.positions().firstOrNull() }
            _nearMe.value = if (fix != null) NearMeState.On(fix) else NearMeState.NoFix
        }
    }

    /** The name filter and position one search runs with; [text] is empty for a near-me search without a name. */
    private data class SearchKey(val text: String, val origin: GeoPosition?, val generation: Int)

    private fun keyOf(text: String, nearMe: NearMeState): SearchKey {
        val origin = (nearMe as? NearMeState.On)?.position
        // Near me ignores a name shorter than the minimum instead of waiting for more characters.
        val filter = if (origin != null && text.length < MIN_SEARCH_LENGTH) "" else text
        return SearchKey(filter, origin, generation)
    }

    @OptIn(FlowPreview::class, ExperimentalCoroutinesApi::class)
    private suspend fun search() {
        combine(_query.map { it.trim() }.debounce(SEARCH_DEBOUNCE_MS), _nearMe, ::keyOf)
            .distinctUntilChanged()
            // A newer query cancels the search still in flight, so its results are never shown.
            .transformLatest { key ->
                if (key.origin == null && key.text.length < MIN_SEARCH_LENGTH) {
                    // Near me switched on but not located yet keeps the last state.
                    if (!_nearMe.value.isOn) emit(SearchState.Idle)
                    return@transformLatest
                }
                emit(SearchState.Searching)
                val result = repository.searchPublic(key.text, key.origin)
                if (result.isFailure) generation++
                // The text or Near me may have changed (or been cleared) while the request ran.
                val current = keyOf(_query.value.trim(), _nearMe.value)
                if (current.text != key.text || current.origin != key.origin) return@transformLatest
                emit(
                    result.fold(
                        onSuccess = { list ->
                            val items = list.map { it.toItem(from = key.origin) }
                            SearchState.Results(if (key.origin != null) items.sortedBy { it.distanceMeters ?: Int.MAX_VALUE } else items)
                        },
                        onFailure = { SearchState.Error },
                    ),
                )
            }
            .collect { _state.value = it }
    }
}
