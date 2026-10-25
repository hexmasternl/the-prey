package nl.hexmaster.theprey.data.sensors

import kotlin.math.abs

/**
 * Smooths compass azimuths: a low-pass filter that turns the short way round the 359° → 0° wrap,
 * and a threshold that drops changes too small to redraw. Pure; one instance per stream.
 */
class HeadingFilter(
    /** Weight of a new reading, 0..1. */
    private val alpha: Float = 0.25f,
    /** Smoothed changes smaller than this (degrees) are not emitted. */
    private val thresholdDegrees: Float = 2f,
) {
    private var smoothed: Float? = null
    private var lastEmitted: Float? = null

    /** Feeds one raw azimuth (degrees); returns the heading to show, or null when it did not change enough. */
    fun accept(rawDegrees: Float): Float? {
        val raw = normalize(rawDegrees)
        val next = smoothed?.let { normalize(it + alpha * shortestDelta(it, raw)) } ?: raw
        smoothed = next
        val previous = lastEmitted
        if (previous != null && abs(shortestDelta(previous, next)) < thresholdDegrees) return null
        lastEmitted = next
        return next
    }

    companion object {
        /** [degrees] in [0, 360). */
        fun normalize(degrees: Float): Float = ((degrees % 360f) + 360f) % 360f

        /** The signed turn from [from] to [to] the short way, in (-180, 180]. */
        fun shortestDelta(from: Float, to: Float): Float {
            val d = normalize(to - from)
            return if (d > 180f) d - 360f else d
        }
    }
}
