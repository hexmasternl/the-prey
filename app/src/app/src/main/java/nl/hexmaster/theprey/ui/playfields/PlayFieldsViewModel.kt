package nl.hexmaster.theprey.ui.playfields

import androidx.lifecycle.SavedStateHandle
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.FlowPreview
import kotlinx.coroutines.Job
import kotlinx.coroutines.channels.Channel
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.debounce
import kotlinx.coroutines.flow.distinctUntilChanged
import kotlinx.coroutines.flow.map
import kotlinx.coroutines.flow.receiveAsFlow
import kotlinx.coroutines.flow.transformLatest
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch
import nl.hexmaster.theprey.data.playfield.DeleteOutcome
import nl.hexmaster.theprey.data.playfield.PlayFieldRepository

/** Quiet time after the last keystroke before the public search is sent. */
const val SEARCH_DEBOUNCE_MS = 366L

/** The server rejects shorter (trimmed) public searches. */
const val MIN_SEARCH_LENGTH = 3

enum class PlayFieldsTab { Private, Public }

/** The player's own playfields (Private tab). */
sealed interface ListState {
    data object Loading : ListState
    data class Loaded(val items: List<PlayFieldItem>) : ListState
    data object Error : ListState
}

/** The public search (Public tab). */
sealed interface SearchState {
    /** Fewer than [MIN_SEARCH_LENGTH] characters: show the hint. */
    data object Idle : SearchState
    data object Searching : SearchState

    /** An empty list is the no-results state. */
    data class Results(val items: List<PlayFieldItem>) : SearchState
    data object Error : SearchState
}

data class PlayFieldsUiState(
    val selectedTab: PlayFieldsTab = PlayFieldsTab.Private,
    val mine: ListState = ListState.Loading,
    /** Own playfields with a delete request in flight: shown busy, not deletable again. */
    val deletingIds: Set<String> = emptySet(),
    /** Drives the delete confirmation dialog. */
    val pendingDelete: PlayFieldItem? = null,
    val query: String = "",
    val search: SearchState = SearchState.Idle,
)

/**
 * The playfields list: own playfields with delete, and the debounced public search. Scoped to
 * the Playfields back-stack entry, so every open from the main menu starts on the Private tab;
 * the tab and the search text survive a configuration change through [savedStateHandle].
 */
class PlayFieldsViewModel(
    private val repository: PlayFieldRepository,
    private val savedStateHandle: SavedStateHandle,
) : ViewModel() {

    private val query = MutableStateFlow(savedStateHandle.get<String>(KEY_QUERY).orEmpty())

    private val _uiState = MutableStateFlow(
        PlayFieldsUiState(
            selectedTab = savedStateHandle.get<String>(KEY_TAB)
                ?.let { saved -> PlayFieldsTab.entries.firstOrNull { it.name == saved } }
                ?: PlayFieldsTab.Private,
            query = query.value,
        ),
    )
    val uiState: StateFlow<PlayFieldsUiState> = _uiState.asStateFlow()

    private val deleteFailures = Channel<Unit>(Channel.BUFFERED)

    /** One event per delete the server did not carry out; shown as a snackbar. */
    val deleteFailed: Flow<Unit> = deleteFailures.receiveAsFlow()

    private var mineRequest: Job? = null

    /**
     * Bumped when the same trimmed text must be searched again: after a failed search, and after
     * the results were cleared below [MIN_SEARCH_LENGTH] (the debounce may never see the short text).
     */
    private var searchGeneration = 0

    init {
        refreshMine()
        viewModelScope.launch { publicSearch() }
    }

    fun selectTab(tab: PlayFieldsTab) {
        savedStateHandle[KEY_TAB] = tab.name
        _uiState.update { it.copy(selectedTab = tab) }
    }

    /**
     * Downloads the own playfields. Only the first load (or a retry after an error) shows
     * [ListState.Loading]; a refresh keeps the shown items until the new list arrives, and keeps
     * them when it fails. A load already in flight is not started again.
     */
    fun refreshMine() {
        if (mineRequest?.isActive == true) return
        if (_uiState.value.mine !is ListState.Loaded) _uiState.update { it.copy(mine = ListState.Loading) }
        mineRequest = viewModelScope.launch {
            val result = repository.listMine()
            _uiState.update { state ->
                result.fold(
                    onSuccess = { list -> state.copy(mine = ListState.Loaded(list.map { it.toItem() })) },
                    onFailure = { if (state.mine is ListState.Loaded) state else state.copy(mine = ListState.Error) },
                )
            }
        }
    }

    /** Opens the confirmation for [item]; nothing is sent yet. */
    fun requestDelete(item: PlayFieldItem) {
        if (item.id in _uiState.value.deletingIds) return
        _uiState.update { it.copy(pendingDelete = item) }
    }

    fun cancelDelete() {
        _uiState.update { it.copy(pendingDelete = null) }
    }

    /** Deletes the confirmed playfield; the item is only removed once the server reports it gone. */
    fun confirmDelete() {
        val item = _uiState.value.pendingDelete ?: return
        if (item.id in _uiState.value.deletingIds) {
            cancelDelete()
            return
        }
        _uiState.update { it.copy(pendingDelete = null, deletingIds = it.deletingIds + item.id) }
        viewModelScope.launch {
            val outcome = repository.delete(item.id)
            _uiState.update { state ->
                val mine = state.mine
                state.copy(
                    deletingIds = state.deletingIds - item.id,
                    mine = if (outcome == DeleteOutcome.Deleted && mine is ListState.Loaded) {
                        ListState.Loaded(mine.items.filterNot { it.id == item.id })
                    } else {
                        mine
                    },
                )
            }
            if (outcome == DeleteOutcome.Failed) deleteFailures.send(Unit)
        }
    }

    fun onQueryChange(text: String) {
        savedStateHandle[KEY_QUERY] = text
        query.value = text
        if (text.trim().length < MIN_SEARCH_LENGTH) {
            // Cleared at once, not after the debounce.
            searchGeneration++
            _uiState.update { it.copy(query = text, search = SearchState.Idle) }
        } else {
            _uiState.update { it.copy(query = text) }
        }
    }

    private data class SearchKey(val text: String, val generation: Int)

    @OptIn(FlowPreview::class, ExperimentalCoroutinesApi::class)
    private suspend fun publicSearch() {
        query
            .map { it.trim() }
            .debounce(SEARCH_DEBOUNCE_MS)
            .map { SearchKey(it, searchGeneration) }
            .distinctUntilChanged()
            // A newer query cancels the search still in flight, so its results are never shown.
            .transformLatest { key ->
                if (key.text.length < MIN_SEARCH_LENGTH) {
                    emit(SearchState.Idle)
                    return@transformLatest
                }
                emit(SearchState.Searching)
                val result = repository.searchPublic(key.text)
                if (result.isFailure) searchGeneration++
                // The text may have changed (or been cleared) while the request ran.
                if (query.value.trim() != key.text) return@transformLatest
                emit(
                    result.fold(
                        onSuccess = { list -> SearchState.Results(list.map { it.toItem() }) },
                        onFailure = { SearchState.Error },
                    ),
                )
            }
            .collect { search -> _uiState.update { it.copy(search = search) } }
    }

    private companion object {
        const val KEY_TAB = "playfields_tab"
        const val KEY_QUERY = "playfields_query"
    }
}
