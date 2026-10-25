package nl.hexmaster.theprey.ui.game.hud.common

import nl.hexmaster.theprey.data.location.GeoPosition
import kotlin.math.atan2
import kotlin.math.cos
import kotlin.math.roundToInt
import kotlin.math.sin
import kotlin.math.sqrt

private const val EARTH_RADIUS_METERS = 6_371_000.0

/** Great-circle (haversine) distance between [a] and [b] in meters. */
fun distanceMeters(a: GeoPosition, b: GeoPosition): Double {
    val lat1 = Math.toRadians(a.latitude)
    val lat2 = Math.toRadians(b.latitude)
    val dLat = lat2 - lat1
    val dLon = Math.toRadians(b.longitude - a.longitude)
    val h = sin(dLat / 2) * sin(dLat / 2) + cos(lat1) * cos(lat2) * sin(dLon / 2) * sin(dLon / 2)
    return 2 * EARTH_RADIUS_METERS * atan2(sqrt(h), sqrt(1 - h))
}

/** Initial bearing from [from] to [to] in degrees clockwise from north, in [0, 360). */
fun bearingDegrees(from: GeoPosition, to: GeoPosition): Float {
    val lat1 = Math.toRadians(from.latitude)
    val lat2 = Math.toRadians(to.latitude)
    val dLon = Math.toRadians(to.longitude - from.longitude)
    val y = sin(dLon) * cos(lat2)
    val x = cos(lat1) * sin(lat2) - sin(lat1) * cos(lat2) * cos(dLon)
    return ((Math.toDegrees(atan2(y, x)) + 360.0) % 360.0).toFloat()
}

/** The rounded distance from [own] to the nearest of [targets], or null without an own fix or targets. */
fun nearestMeters(own: GeoPosition?, targets: List<GeoPosition>): Int? {
    if (own == null) return null
    return targets.minOfOrNull { distanceMeters(own, it) }?.roundToInt()
}
