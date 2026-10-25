package nl.hexmaster.theprey.ui.playfields.details

import androidx.lifecycle.SavedStateHandle
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import kotlinx.coroutines.Job
import kotlinx.coroutines.channels.Channel
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.receiveAsFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch
import nl.hexmaster.theprey.data.location.GeoPosition
import nl.hexmaster.theprey.data.playfield.LoadOutcome
import nl.hexmaster.theprey.data.playfield.PlayFieldDto
import nl.hexmaster.theprey.data.playfield.PlayFieldRepository
import nl.hexmaster.theprey.data.playfield.SaveOutcome
import nl.hexmaster.theprey.data.playfield.UpsertPlayFieldRequest
import nl.hexmaster.theprey.data.playfield.isPublicEligibleName
import nl.hexmaster.theprey.data.playfield.positions
import nl.hexmaster.theprey.data.playfield.toGpsCoordinateDto

/** The download of the playfield. */
enum class DetailsLoad { Loading, Loaded, NotFound, Error }

/** The editable part of a playfield. */
data class PlayFieldDraft(
    val name: String,
    val isPublic: Boolean,
    val points: List<GeoPosition>,
)

fun PlayFieldDto.toDraft(): PlayFieldDraft = PlayFieldDraft(name = name, isPublic = isPublic, points = positions())

data class PlayFieldDetailsUiState(
    val editable: Boolean,
    val load: DetailsLoad = DetailsLoad.Loading,
    val name: String = "",
    val isPublic: Boolean = false,
    val points: List<GeoPosition> = emptyList(),
    val saving: Boolean = false,
    /** What the server has; null until loaded. */
    val original: PlayFieldDraft? = null,
) {
    /** The Public checkbox can be changed: only for a public-eligible name, and never read-only. */
    val publicEnabled: Boolean get() = editable && isPublicEligibleName(name)

    /** Name (trimmed), visibility or area differs from what was loaded. */
    val isDirty: Boolean
        get() = original != null &&
            (name.trim() != original.name.trim() || isPublic != original.isPublic || points != original.points)

    val canSave: Boolean get() = editable && load == DetailsLoad.Loaded && !saving && name.isNotBlank() && isDirty
}

/** One-shot results of a save, for the screen to act on once. */
sealed interface DetailsEvent {
    /** Saved: leave the screen; the list reloads when it resumes. */
    data object Closed : DetailsEvent

    /** Not saved; the edits are kept. */
    data object SaveFailed : DetailsEvent

    /** Changed elsewhere: the newer version replaced the edits. */
    data object Conflict : DetailsEvent
}

/**
 * One playfield's details and its unsaved draft (name, visibility, area). Scoped to the playfield's
 * nested navigation graph, so the area screen edits the same draft through [applyArea], and leaving
 * the graph (Save, Cancel, Back) discards it. Opened from the Public tab it is read-only ([editable]
 * false) and ignores every edit. The draft is mirrored in [savedStateHandle] and laid over a fresh
 * download after process death.
 */
class PlayFieldDetailsViewModel(
    private val playFieldId: String,
    editable: Boolean,
    private val repository: PlayFieldRepository,
    private val savedStateHandle: SavedStateHandle,
) : ViewModel() {

    private val _uiState = MutableStateFlow(PlayFieldDetailsUiState(editable = editable))
    val uiState: StateFlow<PlayFieldDetailsUiState> = _uiState.asStateFlow()

    private val events = Channel<DetailsEvent>(Channel.BUFFERED)
    val detailsEvents: Flow<DetailsEvent> = events.receiveAsFlow()

    /** The version the edit started from; its last-updated time goes back to the server untouched. */
    private var loaded: PlayFieldDto? = null

    private var loadRequest: Job? = null

    init {
        load()
    }

    /** Downloads the playfield again after an error or a not-found. */
    fun retry() {
        if (_uiState.value.load == DetailsLoad.Error || _uiState.value.load == DetailsLoad.NotFound) load()
    }

    private fun load() {
        if (loadRequest?.isActive == true) return
        _uiState.update { it.copy(load = DetailsLoad.Loading) }
        loadRequest = viewModelScope.launch {
            when (val outcome = repository.get(playFieldId)) {
                is LoadOutcome.Found -> {
                    loaded = outcome.playField
                    val original = outcome.playField.toDraft()
                    // After process death the player's unsaved edits are laid over the fresh download.
                    val draft = savedDraft() ?: original
                    showDraft(draft, original)
                }
                LoadOutcome.NotFound -> _uiState.update { it.copy(load = DetailsLoad.NotFound) }
                LoadOutcome.Failed -> _uiState.update { it.copy(load = DetailsLoad.Error) }
            }
        }
    }

    fun onNameChange(name: String) {
        if (!canEdit()) return
        // An ineligible name can't be public: the server would force it private anyway.
        updateDraft { it.copy(name = name, isPublic = it.isPublic && isPublicEligibleName(name)) }
    }

    fun onPublicChange(isPublic: Boolean) {
        if (!canEdit() || !_uiState.value.publicEnabled) return
        updateDraft { it.copy(isPublic = isPublic) }
    }

    /** The area saved on the area screen; an unsaved change until [save]. */
    fun applyArea(points: List<GeoPosition>) {
        if (!canEdit()) return
        updateDraft { it.copy(points = points.toList()) }
    }

    /** Sends name, visibility and points in one update, from the loaded version's last-updated time. */
    fun save() {
        val state = _uiState.value
        val base = loaded ?: return
        if (!state.canSave) return
        _uiState.update { it.copy(saving = true) }
        val request = UpsertPlayFieldRequest(
            name = state.name.trim(),
            isPublic = state.isPublic && isPublicEligibleName(state.name),
            points = state.points.map { it.toGpsCoordinateDto() },
            lastUpdatedOn = base.lastUpdatedOn,
        )
        viewModelScope.launch {
            when (val outcome = repository.save(playFieldId, request)) {
                is SaveOutcome.Saved -> {
                    // Show what the server keeps (it may have forced the playfield private).
                    loaded = outcome.playField
                    val stored = outcome.playField.toDraft()
                    showDraft(stored, stored)
                    events.send(DetailsEvent.Closed)
                }
                is SaveOutcome.Conflict -> {
                    loaded = outcome.current
                    val current = outcome.current.toDraft()
                    showDraft(current, current)
                    events.send(DetailsEvent.Conflict)
                }
                SaveOutcome.Failed -> {
                    _uiState.update { it.copy(saving = false) }
                    events.send(DetailsEvent.SaveFailed)
                }
            }
        }
    }

    private fun canEdit(): Boolean = _uiState.value.let { it.editable && it.load == DetailsLoad.Loaded && !it.saving }

    private fun showDraft(draft: PlayFieldDraft, original: PlayFieldDraft) {
        _uiState.update {
            it.copy(
                load = DetailsLoad.Loaded,
                name = draft.name,
                isPublic = draft.isPublic,
                points = draft.points,
                original = original,
                saving = false,
            )
        }
        saveDraft(draft)
    }

    private fun updateDraft(change: (PlayFieldDraft) -> PlayFieldDraft) {
        val state = _uiState.value
        val draft = change(PlayFieldDraft(state.name, state.isPublic, state.points))
        _uiState.update { it.copy(name = draft.name, isPublic = draft.isPublic, points = draft.points) }
        saveDraft(draft)
    }

    private fun saveDraft(draft: PlayFieldDraft) {
        savedStateHandle[KEY_NAME] = draft.name
        savedStateHandle[KEY_PUBLIC] = draft.isPublic
        savedStateHandle[KEY_POINTS] = draft.points.toFlatArray()
    }

    private fun savedDraft(): PlayFieldDraft? {
        val name = savedStateHandle.get<String>(KEY_NAME) ?: return null
        return PlayFieldDraft(
            name = name,
            isPublic = savedStateHandle.get<Boolean>(KEY_PUBLIC) ?: false,
            points = savedStateHandle.get<DoubleArray>(KEY_POINTS)?.toPositions().orEmpty(),
        )
    }

    private companion object {
        const val KEY_NAME = "playfield_draft_name"
        const val KEY_PUBLIC = "playfield_draft_public"
        const val KEY_POINTS = "playfield_draft_points"
    }
}

/** Points as `[lat0, lon0, lat1, lon1, …]`, a type a SavedStateHandle can keep. */
fun List<GeoPosition>.toFlatArray(): DoubleArray =
    DoubleArray(size * 2) { i -> this[i / 2].let { if (i % 2 == 0) it.latitude else it.longitude } }

fun DoubleArray.toPositions(): List<GeoPosition> = (0 until size / 2).map { GeoPosition(this[it * 2], this[it * 2 + 1]) }
