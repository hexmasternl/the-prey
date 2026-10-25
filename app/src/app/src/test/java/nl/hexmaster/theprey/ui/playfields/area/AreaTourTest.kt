package nl.hexmaster.theprey.ui.playfields.area

import nl.hexmaster.theprey.R
import org.junit.Assert.assertEquals
import org.junit.Test

class AreaTourTest {

    @Test
    fun areaTourSteps_ShouldFollowTheEditorInOrder_WhenTheTourRuns() {
        assertEquals(
            listOf(
                R.string.tour_area_intro_title,
                R.string.tour_area_add_title,
                R.string.tour_area_select_title,
                R.string.tour_area_delete_title,
                R.string.tour_area_erase_title,
                R.string.tour_area_save_title,
            ),
            AreaTourSteps.map { it.title },
        )
    }

    @Test
    fun areaTourSteps_ShouldSpotlightDeleteEraseAndSave_WhenThoseStepsShow() {
        assertEquals(
            listOf(null, null, null, AreaTourTargets.DELETE_POINT, AreaTourTargets.ERASE_ALL, AreaTourTargets.SAVE),
            AreaTourSteps.map { it.targetKey },
        )
    }
}
