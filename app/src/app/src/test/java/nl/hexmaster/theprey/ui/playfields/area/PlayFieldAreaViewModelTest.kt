package nl.hexmaster.theprey.ui.playfields.area

import androidx.lifecycle.SavedStateHandle
import nl.hexmaster.theprey.data.location.GeoPosition
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

class PlayFieldAreaViewModelTest {

    private val a = GeoPosition(52.36, 4.86)
    private val b = GeoPosition(52.36, 4.88)
    private val c = GeoPosition(52.35, 4.88)
    private val d = GeoPosition(52.35, 4.86)

    private val applied = mutableListOf<List<GeoPosition>>()

    private fun viewModel(
        initial: List<GeoPosition> = emptyList(),
        editable: Boolean = true,
        handle: SavedStateHandle = SavedStateHandle(),
    ) = PlayFieldAreaViewModel(initial, editable, handle) { applied += it }

    @Test
    fun onMapTapped_ShouldAddAPoint_WhenNothingIsSelected() {
        val vm = viewModel()

        vm.onMapTapped(a)

        assertEquals(listOf(a), vm.uiState.value.points)
        assertNull(vm.uiState.value.selectedIndex)
    }

    @Test
    fun onMapTapped_ShouldAppendInPlacingOrder_WhenTappedAgain() {
        val vm = viewModel(initial = listOf(a))

        vm.onMapTapped(b)
        vm.onMapTapped(c)

        assertEquals(listOf(a, b, c), vm.uiState.value.points)
    }

    @Test
    fun onMapTapped_ShouldOnlyDeselect_WhenAPointIsSelected() {
        val vm = viewModel(initial = listOf(a, b))
        vm.onPointTapped(1)

        vm.onMapTapped(c)

        assertNull(vm.uiState.value.selectedIndex)
        assertEquals(listOf(a, b), vm.uiState.value.points)
    }

    @Test
    fun onPointTapped_ShouldSelectOnlyThatPoint_WhenAnotherWasSelected() {
        val vm = viewModel(initial = listOf(a, b, c))

        vm.onPointTapped(0)
        vm.onPointTapped(2)

        assertEquals(2, vm.uiState.value.selectedIndex)
        assertEquals("tapping a point adds nothing", 3, vm.uiState.value.points.size)
    }

    @Test
    fun onPointTapped_ShouldBeIgnored_WhenIndexIsOutOfRange() {
        val vm = viewModel(initial = listOf(a))

        vm.onPointTapped(3)

        assertNull(vm.uiState.value.selectedIndex)
    }

    @Test
    fun onDragSelected_ShouldMoveTheSelectedPointAndKeepItSelected_WhenDragging() {
        val vm = viewModel(initial = listOf(a, b, c))
        vm.onPointTapped(1)

        vm.onDragSelected(GeoPosition(52.37, 4.87))
        vm.onDragSelected(d)

        assertEquals(listOf(a, d, c), vm.uiState.value.points)
        assertEquals(1, vm.uiState.value.selectedIndex)
    }

    @Test
    fun onDragSelected_ShouldBeIgnored_WhenNothingIsSelected() {
        val vm = viewModel(initial = listOf(a, b, c))

        vm.onDragSelected(d)

        assertEquals(listOf(a, b, c), vm.uiState.value.points)
    }

    @Test
    fun canSave_ShouldNeedThreePoints_WhenEditable() {
        val vm = viewModel()
        assertFalse(vm.uiState.value.canSave)
        assertFalse(vm.uiState.value.canErase)

        vm.onMapTapped(a)
        vm.onMapTapped(b)
        assertFalse(vm.uiState.value.canSave)
        assertTrue(vm.uiState.value.canErase)

        vm.onMapTapped(c)
        assertTrue(vm.uiState.value.canSave)
    }

    @Test
    fun eraseAll_ShouldClearPointsAndSelectionAndDisableSave_WhenThereArePoints() {
        val vm = viewModel(initial = listOf(a, b, c, d, a.copy(latitude = 52.355)))
        vm.onPointTapped(2)

        vm.eraseAll()

        val state = vm.uiState.value
        assertTrue(state.points.isEmpty())
        assertNull(state.selectedIndex)
        assertFalse(state.canSave)
        assertFalse(state.canErase)
    }

    @Test
    fun save_ShouldHandThePointsToTheDetails_WhenThereAreEnough() {
        val vm = viewModel()
        listOf(a, b, c, d).forEach(vm::onMapTapped)

        assertTrue(vm.save())

        assertEquals(listOf(listOf(a, b, c, d)), applied)
    }

    @Test
    fun save_ShouldDoNothing_WhenFewerThanThreePoints() {
        val vm = viewModel(initial = listOf(a, b))

        assertFalse(vm.save())

        assertTrue(applied.isEmpty())
    }

    @Test
    fun edits_ShouldBeIgnored_WhenReadOnly() {
        val vm = viewModel(initial = listOf(a, b, c), editable = false)

        vm.onMapTapped(d)
        vm.onPointTapped(0)
        vm.onDragSelected(d)
        vm.eraseAll()

        val state = vm.uiState.value
        assertEquals(listOf(a, b, c), state.points)
        assertNull(state.selectedIndex)
        assertFalse(state.canSave)
        assertFalse(state.canErase)
        assertFalse(vm.save())
        assertTrue(applied.isEmpty())
    }

    @Test
    fun init_ShouldRestorePointsAndSelection_WhenSavedStateExists() {
        val handle = SavedStateHandle()
        viewModel(initial = listOf(a), handle = handle).apply {
            onMapTapped(b)
            onMapTapped(c)
            onMapTapped(d)
            onPointTapped(3)
        }

        // Recreated with the restored handle: the details' (older) points are not used.
        val restored = viewModel(initial = listOf(a), handle = handle)

        assertEquals(listOf(a, b, c, d), restored.uiState.value.points)
        assertEquals(3, restored.uiState.value.selectedIndex)
    }

    @Test
    fun init_ShouldRestoreAnErasedArea_WhenSavedStateHasNoPoints() {
        val handle = SavedStateHandle()
        viewModel(initial = listOf(a, b, c), handle = handle).eraseAll()

        val restored = viewModel(initial = listOf(a, b, c), handle = handle)

        assertTrue(restored.uiState.value.points.isEmpty())
    }

    // ---- Delete the selected point ----

    private val e = GeoPosition(52.355, 4.87)

    @Test
    fun canDeleteSelected_ShouldFollowTheSelection_WhenPointsAreTappedAndDeselected() {
        val vm = viewModel(initial = listOf(a, b, c))
        assertFalse("nothing selected", vm.uiState.value.canDeleteSelected)

        vm.onPointTapped(1)
        assertTrue(vm.uiState.value.canDeleteSelected)

        vm.onMapTapped(d)
        assertFalse("deselected", vm.uiState.value.canDeleteSelected)
    }

    @Test
    fun canDeleteSelected_ShouldBeFalse_WhenReadOnly() {
        val vm = viewModel(initial = listOf(a, b, c), editable = false)

        vm.onPointTapped(1)

        assertFalse(vm.uiState.value.canDeleteSelected)
    }

    @Test
    fun deleteSelected_ShouldRemoveOnlyThatPointAndKeepTheOrder_WhenTheMiddleOfFiveIsSelected() {
        val vm = viewModel(initial = listOf(a, b, c, d, e))
        vm.onPointTapped(2)

        vm.deleteSelected()

        assertEquals(listOf(a, b, d, e), vm.uiState.value.points)
        assertNull(vm.uiState.value.selectedIndex)
        assertFalse(vm.uiState.value.canDeleteSelected)
    }

    @Test
    fun deleteSelected_ShouldDisableSave_WhenFewerThanThreePointsRemain() {
        val vm = viewModel(initial = listOf(a, b, c))
        vm.onPointTapped(0)

        vm.deleteSelected()

        assertEquals(listOf(b, c), vm.uiState.value.points)
        assertFalse(vm.uiState.value.canSave)
    }

    @Test
    fun deleteSelected_ShouldDisableErase_WhenTheOnlyPointIsDeleted() {
        val vm = viewModel(initial = listOf(a))
        vm.onPointTapped(0)

        vm.deleteSelected()

        assertTrue(vm.uiState.value.points.isEmpty())
        assertFalse(vm.uiState.value.canErase)
    }

    @Test
    fun deleteSelected_ShouldDoNothing_WhenNothingIsSelectedOrReadOnly() {
        val editable = viewModel(initial = listOf(a, b, c))
        editable.deleteSelected()
        assertEquals(listOf(a, b, c), editable.uiState.value.points)

        val readOnly = viewModel(initial = listOf(a, b, c), editable = false)
        readOnly.onPointTapped(1)
        readOnly.deleteSelected()
        assertEquals(listOf(a, b, c), readOnly.uiState.value.points)
    }

    @Test
    fun deleteSelected_ShouldSurviveProcessDeath_WhenRestoredFromTheSameHandle() {
        val handle = SavedStateHandle()
        viewModel(initial = listOf(a, b, c, d), handle = handle).apply {
            onPointTapped(1)
            deleteSelected()
        }

        val restored = viewModel(initial = listOf(a, b, c, d), handle = handle).uiState.value

        assertEquals(listOf(a, c, d), restored.points)
        assertNull(restored.selectedIndex)
    }
}
