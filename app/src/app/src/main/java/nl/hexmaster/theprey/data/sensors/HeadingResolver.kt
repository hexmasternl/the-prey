package nl.hexmaster.theprey.data.sensors

import nl.hexmaster.theprey.data.location.GeoPosition
import nl.hexmaster.theprey.ui.game.hud.common.bearingDegrees
import nl.hexmaster.theprey.ui.game.hud.common.distanceMeters

/**
 * Picks the heading the player arrow shows: the compass when there is one, else the direction of
 * travel between the last two fixes when they are at least [MIN_TRAVEL_METERS] apart, else the
 * last heading shown, else north. Pure; one instance per screen.
 */
class HeadingResolver {
    private var previousFix: GeoPosition? = null
    private var lastFix: GeoPosition? = null
    private var lastHeading: Float? = null

    /** Records a new device fix; only fixes [MIN_TRAVEL_METERS] or more from the last one count as travel. */
    fun onFix(fix: GeoPosition) {
        val last = lastFix
        if (last == null) {
            lastFix = fix
        } else if (distanceMeters(last, fix) >= MIN_TRAVEL_METERS) {
            previousFix = last
            lastFix = fix
        }
    }

    /** The heading in degrees [0, 360) for the current [compass] reading (null: no usable compass). */
    fun resolve(compass: Float?): Float {
        val travel = previousFix?.let { from -> lastFix?.let { to -> bearingDegrees(from, to) } }
        val heading = compass ?: travel ?: lastHeading ?: 0f
        lastHeading = heading
        return heading
    }

    companion object {
        const val MIN_TRAVEL_METERS = 5.0
    }
}
