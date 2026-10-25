package nl.hexmaster.theprey.ui.playfields

import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Job
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch
import nl.hexmaster.theprey.data.playfield.PlayFieldRepository

/** The player's own playfields (Private tab). */
sealed interface ListState {
    data object Loading : ListState
    data class Loaded(val items: List<PlayFieldItem>) : ListState
    data object Error : ListState
}

/**
 * The player's own playfields, downloaded from the server. Shared by the playfields list and the
 * playfield selection screen; [scope] is the owning view model's scope.
 */
class OwnPlayFields(
    private val repository: PlayFieldRepository,
    private val scope: CoroutineScope,
) {
    private val _state = MutableStateFlow<ListState>(ListState.Loading)
    val state: StateFlow<ListState> = _state.asStateFlow()

    private var request: Job? = null

    /**
     * Downloads the own playfields. Only the first load (or a retry after an error) shows
     * [ListState.Loading]; a refresh keeps the shown items until the new list arrives, and keeps
     * them when it fails. A load already in flight is not started again.
     */
    fun refresh() {
        if (request?.isActive == true) return
        if (_state.value !is ListState.Loaded) _state.value = ListState.Loading
        request = scope.launch {
            val result = repository.listMine()
            _state.update { state ->
                result.fold(
                    onSuccess = { list -> ListState.Loaded(list.map { it.toItem() }) },
                    onFailure = { if (state is ListState.Loaded) state else ListState.Error },
                )
            }
        }
    }

    /** Drops [id] from the shown list, e.g. after the server confirmed its delete. */
    fun remove(id: String) {
        _state.update { state ->
            if (state is ListState.Loaded) ListState.Loaded(state.items.filterNot { it.id == id }) else state
        }
    }
}
