package nl.hexmaster.theprey.ui.game.hud.hunter

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
import nl.hexmaster.theprey.data.game.GameRepository
import nl.hexmaster.theprey.data.game.GameSessions
import nl.hexmaster.theprey.data.game.TagCandidateDto
import nl.hexmaster.theprey.data.game.TagCandidatesResult
import nl.hexmaster.theprey.data.game.TagResult

/** The tag sheet. */
sealed interface TagSheetState {
    data object Loading : TagSheetState

    /** Preys in range, nearest first; one may be [selectedId]. [sendFailed] after a tag without an answer. */
    data class Candidates(
        val rangeMeters: Double,
        val candidates: List<TagCandidateDto>,
        val selectedId: String? = null,
        val submitting: Boolean = false,
        val sendFailed: Boolean = false,
    ) : TagSheetState {
        val canConfirm: Boolean get() = selectedId != null && !submitting
    }

    /** Nobody within [rangeMeters]: no tag action. */
    data class Empty(val rangeMeters: Double) : TagSheetState

    data object LoadFailed : TagSheetState
}

sealed interface TagSheetEvent {
    /** The server tagged [callsign]: close the sheet and confirm. */
    data class Tagged(val callsign: String) : TagSheetEvent

    /** The server refused (409): the list was reloaded. */
    data object NoLongerTaggable : TagSheetEvent
}

/**
 * Lists the preys the server says are in tagging range and tags one. The server list is the
 * truth: nothing is filtered locally. A successful tag asks the Game State Service for one fresh
 * snapshot, so the HUD counts change at once. Survives rotation, so an in-flight tag is neither
 * lost nor sent twice.
 */
class TagSheetViewModel(
    private val gameId: String,
    private val games: GameRepository,
    private val sessions: GameSessions,
) : ViewModel() {

    private val _state = MutableStateFlow<TagSheetState>(TagSheetState.Loading)
    val state: StateFlow<TagSheetState> = _state.asStateFlow()

    private val _events = Channel<TagSheetEvent>(Channel.BUFFERED)
    val events: Flow<TagSheetEvent> = _events.receiveAsFlow()

    init {
        load()
    }

    /** (Re)loads the candidates; also the retry and refresh action. Ignored while a tag is in flight. */
    fun load() {
        if ((_state.value as? TagSheetState.Candidates)?.submitting == true) return
        _state.value = TagSheetState.Loading
        viewModelScope.launch {
            _state.value = when (val result = games.tagCandidates(gameId)) {
                is TagCandidatesResult.Ok -> {
                    val list = result.candidates.candidates.sortedBy { it.distanceMeters }
                    if (list.isEmpty()) {
                        TagSheetState.Empty(result.candidates.rangeMeters)
                    } else {
                        TagSheetState.Candidates(result.candidates.rangeMeters, list)
                    }
                }
                TagCandidatesResult.Failed -> TagSheetState.LoadFailed
            }
        }
    }

    fun select(userId: String) {
        _state.update { state ->
            if (state is TagSheetState.Candidates && !state.submitting) state.copy(selectedId = userId, sendFailed = false) else state
        }
    }

    fun confirm() {
        val state = _state.value as? TagSheetState.Candidates ?: return
        if (!state.canConfirm) return
        val target = state.candidates.firstOrNull { it.userId == state.selectedId } ?: return
        _state.value = state.copy(submitting = true, sendFailed = false)
        viewModelScope.launch {
            when (games.tag(gameId, target.userId)) {
                TagResult.Tagged -> {
                    sessions.resync(gameId)
                    _events.send(TagSheetEvent.Tagged(target.callsign))
                }
                TagResult.NoLongerTaggable -> {
                    _state.value = state.copy(submitting = false)
                    _events.send(TagSheetEvent.NoLongerTaggable)
                    load()
                }
                TagResult.Failed -> _state.value = state.copy(submitting = false, sendFailed = true)
            }
        }
    }
}
