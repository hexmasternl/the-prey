package nl.hexmaster.theprey.ui.playfields.area

import nl.hexmaster.theprey.R
import nl.hexmaster.theprey.ui.components.tour.TourStep

/** Tour target keys on the area screen. */
object AreaTourTargets {
    const val DELETE_POINT = "area.delete_point"
    const val ERASE_ALL = "area.erase_all"
    const val SAVE = "area.save"
}

/** The one-time walkthrough of the area editor, shown on a new playfield's area. */
val AreaTourSteps: List<TourStep> = listOf(
    TourStep(null, R.string.tour_area_intro_title, R.string.tour_area_intro_body),
    TourStep(null, R.string.tour_area_add_title, R.string.tour_area_add_body),
    TourStep(null, R.string.tour_area_select_title, R.string.tour_area_select_body),
    TourStep(AreaTourTargets.DELETE_POINT, R.string.tour_area_delete_title, R.string.tour_area_delete_body),
    TourStep(AreaTourTargets.ERASE_ALL, R.string.tour_area_erase_title, R.string.tour_area_erase_body),
    TourStep(AreaTourTargets.SAVE, R.string.tour_area_save_title, R.string.tour_area_save_body),
)
