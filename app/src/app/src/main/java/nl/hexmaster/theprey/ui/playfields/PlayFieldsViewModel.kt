package nl.hexmaster.theprey.ui.playfields

import androidx.lifecycle.SavedStateHandle
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import kotlinx.coroutines.channels.Channel
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.receiveAsFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch
import nl.hexmaster.theprey.data.playfield.DeleteOutcome
import nl.hexmaster.theprey.data.playfield.PlayFieldRepository

enum class PlayFieldsTab { Private, Public }

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

/** Reads a saved [PlayFieldsTab] name, falling back to Private. */
internal fun SavedStateHandle.tab(key: String): PlayFieldsTab =
    get<String>(key)?.let { saved -> PlayFieldsTab.entries.firstOrNull { it.name == saved } } ?: PlayFieldsTab.Private

/**
 * The playfields list: own playfields with delete, and the debounced public search. Scoped to
 * the Playfields back-stack entry, so every open from the main menu starts on the Private tab;
 * the tab and the search text survive a configuration change through [savedStateHandle].
 */
class PlayFieldsViewModel(
    private val repository: PlayFieldRepository,
    private val savedStateHandle: SavedStateHandle,
) : ViewModel() {

    private val mine = OwnPlayFields(repository, viewModelScope)
    private val publicSearch = PublicPlayFieldSearch(
        repository,
        viewModelScope,
        initialQuery = savedStateHandle.get<String>(KEY_QUERY).orEmpty(),
    )

    private val _uiState = MutableStateFlow(
        PlayFieldsUiState(selectedTab = savedStateHandle.tab(KEY_TAB), query = publicSearch.query.value),
    )
    val uiState: StateFlow<PlayFieldsUiState> = _uiState.asStateFlow()

    private val deleteFailures = Channel<Unit>(Channel.BUFFERED)

    /** One event per delete the server did not carry out; shown as a snackbar. */
    val deleteFailed: Flow<Unit> = deleteFailures.receiveAsFlow()

    init {
        viewModelScope.launch { mine.state.collect { list -> _uiState.update { it.copy(mine = list) } } }
        viewModelScope.launch { publicSearch.query.collect { text -> _uiState.update { it.copy(query = text) } } }
        viewModelScope.launch { publicSearch.state.collect { search -> _uiState.update { it.copy(search = search) } } }
        refreshMine()
    }

    fun selectTab(tab: PlayFieldsTab) {
        savedStateHandle[KEY_TAB] = tab.name
        _uiState.update { it.copy(selectedTab = tab) }
    }

    /** Downloads the own playfields again; see [OwnPlayFields.refresh]. */
    fun refreshMine() = mine.refresh()

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
            if (outcome == DeleteOutcome.Deleted) mine.remove(item.id)
            _uiState.update { it.copy(deletingIds = it.deletingIds - item.id) }
            if (outcome == DeleteOutcome.Failed) deleteFailures.send(Unit)
        }
    }

    fun onQueryChange(text: String) {
        savedStateHandle[KEY_QUERY] = text
        publicSearch.onQueryChange(text)
    }

    private companion object {
        const val KEY_TAB = "playfields_tab"
        const val KEY_QUERY = "playfields_query"
    }
}
