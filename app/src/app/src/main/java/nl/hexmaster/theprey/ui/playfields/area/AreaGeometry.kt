package nl.hexmaster.theprey.ui.playfields.area

import nl.hexmaster.theprey.data.location.GeoPosition

/** A position on the screen, in pixels. Plain Kotlin so the geometry is testable without Android. */
data class ScreenPoint(val x: Float, val y: Float)

/**
 * The index of the point in [points] nearest to [tap], if it lies within [radiusPx]; null when
 * none does. A radius of 24 dp gives every point a 48 dp touch target.
 */
fun hitTest(points: List<ScreenPoint>, tap: ScreenPoint, radiusPx: Float): Int? {
    val limit = radiusPx * radiusPx
    var best: Int? = null
    var bestDistance = Float.MAX_VALUE
    points.forEachIndexed { index, point ->
        val dx = point.x - tap.x
        val dy = point.y - tap.y
        val distance = dx * dx + dy * dy
        if (distance <= limit && distance < bestDistance) {
            best = index
            bestDistance = distance
        }
    }
    return best
}

/** Where the map camera starts. */
sealed interface MapCamera {
    /** Show the whole area; the map host adds the padding. */
    data class Fit(val southWest: GeoPosition, val northEast: GeoPosition) : MapCamera

    data class Center(val target: GeoPosition, val zoom: Double) : MapCamera
}

/** Street level: a playfield fills a good part of the screen. */
const val STREET_ZOOM = 16.0

/** No area and no device position: the whole world. */
val WorldCamera = MapCamera.Center(GeoPosition(0.0, 0.0), zoom = 1.0)

/**
 * The first camera for an area: fit 2 or more (distinct) points, centre on a single point or the
 * device position at street level, otherwise the world view.
 */
fun initialCamera(points: List<GeoPosition>, devicePosition: GeoPosition?): MapCamera = when {
    points.distinct().size >= 2 -> MapCamera.Fit(
        southWest = GeoPosition(points.minOf { it.latitude }, points.minOf { it.longitude }),
        northEast = GeoPosition(points.maxOf { it.latitude }, points.maxOf { it.longitude }),
    )
    points.isNotEmpty() -> MapCamera.Center(points.first(), STREET_ZOOM)
    devicePosition != null -> MapCamera.Center(devicePosition, STREET_ZOOM)
    else -> WorldCamera
}
