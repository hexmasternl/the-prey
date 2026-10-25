package nl.hexmaster.theprey.ui.playfields.area

import androidx.lifecycle.SavedStateHandle
import androidx.lifecycle.ViewModel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import nl.hexmaster.theprey.data.location.GeoPosition
import nl.hexmaster.theprey.ui.playfields.details.toFlatArray
import nl.hexmaster.theprey.ui.playfields.details.toPositions

/** The server needs at least this many points for a playfield. */
const val MIN_AREA_POINTS = 3

data class PlayFieldAreaUiState(
    /** In placing order; the outline connects them in this order and closes back to the first. */
    val points: List<GeoPosition>,
    /** The point with the red border that can be dragged; null when none is selected. */
    val selectedIndex: Int? = null,
    val editable: Boolean,
) {
    val canSave: Boolean get() = editable && points.size >= MIN_AREA_POINTS
    val canErase: Boolean get() = editable && points.isNotEmpty()

    /** A point is selected that the delete button can remove. */
    val canDeleteSelected: Boolean get() = editable && selectedIndex != null && selectedIndex in points.indices
}

/**
 * The area being drawn on the map, starting from the details screen's current points. Nothing is
 * written anywhere until [save] hands the points to [applyArea] (the details draft); leaving
 * without saving discards them. Read-only ([editable] false) ignores every edit. Points and the
 * selection survive rotation and process death through [savedStateHandle].
 */
class PlayFieldAreaViewModel(
    initialPoints: List<GeoPosition>,
    editable: Boolean,
    private val savedStateHandle: SavedStateHandle,
    private val applyArea: (List<GeoPosition>) -> Unit,
) : ViewModel() {

    private val _uiState = MutableStateFlow(
        PlayFieldAreaUiState(
            points = savedStateHandle.get<DoubleArray>(KEY_POINTS)?.toPositions() ?: initialPoints,
            selectedIndex = savedStateHandle.get<Int>(KEY_SELECTED)?.takeIf { it >= 0 },
            editable = editable,
        ),
    )
    val uiState: StateFlow<PlayFieldAreaUiState> = _uiState.asStateFlow()

    /** A tap on the map away from every point: deselects when a point is selected, otherwise adds one. */
    fun onMapTapped(position: GeoPosition) {
        val state = _uiState.value
        if (!state.editable) return
        if (state.selectedIndex != null) {
            set(state.copy(selectedIndex = null))
        } else {
            set(state.copy(points = state.points + position))
        }
    }

    /** A tap on point [index]: it becomes the (only) selected point. */
    fun onPointTapped(index: Int) {
        val state = _uiState.value
        if (!state.editable || index !in state.points.indices) return
        set(state.copy(selectedIndex = index))
    }

    /** The selected point is dragged to [position]; it stays selected. */
    fun onDragSelected(position: GeoPosition) {
        val state = _uiState.value
        val index = state.selectedIndex ?: return
        if (!state.editable || index !in state.points.indices) return
        set(state.copy(points = state.points.toMutableList().also { it[index] = position }))
    }

    /** Removes the selected point; the rest keep their order and nothing is selected afterwards. */
    fun deleteSelected() {
        val state = _uiState.value
        if (!state.canDeleteSelected) return
        val index = state.selectedIndex ?: return
        set(state.copy(points = state.points.filterIndexed { i, _ -> i != index }, selectedIndex = null))
    }

    fun eraseAll() {
        val state = _uiState.value
        if (!state.canErase) return
        set(state.copy(points = emptyList(), selectedIndex = null))
    }

    /** Hands the points to the details draft. Returns false (and does nothing) when they can't be saved. */
    fun save(): Boolean {
        val state = _uiState.value
        if (!state.canSave) return false
        applyArea(state.points)
        return true
    }

    private fun set(state: PlayFieldAreaUiState) {
        _uiState.value = state
        savedStateHandle[KEY_POINTS] = state.points.toFlatArray()
        savedStateHandle[KEY_SELECTED] = state.selectedIndex ?: -1
    }

    private companion object {
        const val KEY_POINTS = "playfield_area_points"
        const val KEY_SELECTED = "playfield_area_selected"
    }
}
