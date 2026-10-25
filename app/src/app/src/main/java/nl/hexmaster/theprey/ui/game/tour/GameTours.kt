package nl.hexmaster.theprey.ui.game.tour

import nl.hexmaster.theprey.R
import nl.hexmaster.theprey.ui.components.tour.TourStep

/**
 * The hunter's tagging range in metres, as the server applies it (`rangeMeters` in
 * `GET /games/{id}/tag-candidates`). Only used for the tour text; change it together with the server.
 */
const val TAG_RANGE_METERS = 50

/** Tour target keys on the role HUDs. */
object GameTourTargets {
    const val PREY_ROLE = "prey.role"
    const val PREY_PROXIMITY = "prey.proximity"
    const val PREY_GAME_TIME = "prey.game_time"
    const val PREY_HUNTER_DISTANCE = "prey.hunter_distance"
    const val PREY_NEXT_BROADCAST = "prey.next_broadcast"
    const val PREY_TOGGLE = "prey.toggle"

    const val HUNTER_ROLE = "hunter.role"
    const val HUNTER_CLOCK = "hunter.clock"
    const val HUNTER_NEXT_PING = "hunter.next_ping"
    const val HUNTER_PREYS_IN_PLAY = "hunter.preys_in_play"
    const val HUNTER_CLOSEST_PREY = "hunter.closest_prey"
    const val HUNTER_PENALISED = "hunter.penalised"
    const val HUNTER_TAG = "hunter.tag"
}

/** The prey HUD tour: every panel, top to bottom. */
val PreyTourSteps: List<TourStep> = listOf(
    TourStep(GameTourTargets.PREY_ROLE, R.string.tour_prey_role_title, R.string.tour_prey_role_body),
    TourStep(GameTourTargets.PREY_PROXIMITY, R.string.tour_prey_proximity_title, R.string.tour_prey_proximity_body),
    TourStep(GameTourTargets.PREY_GAME_TIME, R.string.tour_prey_time_title, R.string.tour_prey_time_body),
    TourStep(GameTourTargets.PREY_HUNTER_DISTANCE, R.string.tour_prey_distance_title, R.string.tour_prey_distance_body),
    TourStep(GameTourTargets.PREY_NEXT_BROADCAST, R.string.tour_prey_broadcast_title, R.string.tour_prey_broadcast_body),
    TourStep(GameTourTargets.PREY_TOGGLE, R.string.tour_prey_toggle_title, R.string.tour_prey_toggle_body),
)

/** The hunter HUD tour: every panel, ending on the Tag button. */
val HunterTourSteps: List<TourStep> = listOf(
    TourStep(GameTourTargets.HUNTER_ROLE, R.string.tour_hunter_role_title, R.string.tour_hunter_role_body),
    TourStep(GameTourTargets.HUNTER_CLOCK, R.string.tour_hunter_clock_title, R.string.tour_hunter_clock_body),
    TourStep(GameTourTargets.HUNTER_NEXT_PING, R.string.tour_hunter_ping_title, R.string.tour_hunter_ping_body),
    TourStep(GameTourTargets.HUNTER_PREYS_IN_PLAY, R.string.tour_hunter_preys_title, R.string.tour_hunter_preys_body),
    TourStep(GameTourTargets.HUNTER_CLOSEST_PREY, R.string.tour_hunter_closest_title, R.string.tour_hunter_closest_body),
    TourStep(GameTourTargets.HUNTER_PENALISED, R.string.tour_hunter_penalised_title, R.string.tour_hunter_penalised_body),
    TourStep(
        GameTourTargets.HUNTER_TAG,
        R.string.tour_hunter_tag_title,
        R.string.tour_hunter_tag_body,
        bodyArgs = listOf(TAG_RANGE_METERS),
    ),
)
