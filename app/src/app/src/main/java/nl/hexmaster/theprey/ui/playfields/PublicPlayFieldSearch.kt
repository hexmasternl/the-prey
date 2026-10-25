package nl.hexmaster.theprey.ui.playfields

import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.FlowPreview
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.debounce
import kotlinx.coroutines.flow.distinctUntilChanged
import kotlinx.coroutines.flow.map
import kotlinx.coroutines.flow.transformLatest
import kotlinx.coroutines.launch
import nl.hexmaster.theprey.data.playfield.PlayFieldRepository

/** Quiet time after the last keystroke before the public search is sent. */
const val SEARCH_DEBOUNCE_MS = 366L

/** The server rejects shorter (trimmed) public searches. */
const val MIN_SEARCH_LENGTH = 3

/** The public search (Public tab). */
sealed interface SearchState {
    /** Fewer than [MIN_SEARCH_LENGTH] characters: show the hint. */
    data object Idle : SearchState
    data object Searching : SearchState

    /** An empty list is the no-results state. */
    data class Results(val items: List<PlayFieldItem>) : SearchState
    data object Error : SearchState
}

/**
 * The debounced search over public playfields. Shared by the playfields list and the playfield
 * selection screen; [scope] is the owning view model's scope and [initialQuery] a restored text.
 */
class PublicPlayFieldSearch(
    private val repository: PlayFieldRepository,
    scope: CoroutineScope,
    initialQuery: String = "",
) {
    private val _query = MutableStateFlow(initialQuery)
    val query: StateFlow<String> = _query.asStateFlow()

    private val _state = MutableStateFlow<SearchState>(SearchState.Idle)
    val state: StateFlow<SearchState> = _state.asStateFlow()

    /**
     * Bumped when the same trimmed text must be searched again: after a failed search, and after
     * the results were cleared below [MIN_SEARCH_LENGTH] (the debounce may never see the short text).
     */
    private var generation = 0

    init {
        scope.launch { search() }
    }

    fun onQueryChange(text: String) {
        if (text.trim().length < MIN_SEARCH_LENGTH) {
            // Cleared at once, not after the debounce.
            generation++
            _state.value = SearchState.Idle
        }
        _query.value = text
    }

    private data class SearchKey(val text: String, val generation: Int)

    @OptIn(FlowPreview::class, ExperimentalCoroutinesApi::class)
    private suspend fun search() {
        _query
            .map { it.trim() }
            .debounce(SEARCH_DEBOUNCE_MS)
            .map { SearchKey(it, generation) }
            .distinctUntilChanged()
            // A newer query cancels the search still in flight, so its results are never shown.
            .transformLatest { key ->
                if (key.text.length < MIN_SEARCH_LENGTH) {
                    emit(SearchState.Idle)
                    return@transformLatest
                }
                emit(SearchState.Searching)
                val result = repository.searchPublic(key.text)
                if (result.isFailure) generation++
                // The text may have changed (or been cleared) while the request ran.
                if (_query.value.trim() != key.text) return@transformLatest
                emit(
                    result.fold(
                        onSuccess = { list -> SearchState.Results(list.map { it.toItem() }) },
                        onFailure = { SearchState.Error },
                    ),
                )
            }
            .collect { _state.value = it }
    }
}
