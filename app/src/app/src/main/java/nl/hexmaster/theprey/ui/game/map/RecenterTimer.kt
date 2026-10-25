package nl.hexmaster.theprey.ui.game.map

/**
 * Decides when the map follows the device again after the player panned or zoomed it: every
 * gesture restarts a [delayMillis] wait. Pure; times are any monotonic milliseconds.
 */
class RecenterTimer(private val delayMillis: Long = RECENTER_DELAY_MS) {

    sealed interface Mode {
        data object Following : Mode
        data class Free(val lastGestureAt: Long) : Mode
    }

    var mode: Mode = Mode.Following
        private set

    val isFollowing: Boolean get() = mode == Mode.Following

    /** The player touched the map (pan, pinch) at [now]. */
    fun onGesture(now: Long) {
        mode = Mode.Free(now)
    }

    /** True once [delayMillis] passed since the last gesture; the timer is then following again. */
    fun shouldRecenter(now: Long): Boolean {
        val free = mode as? Mode.Free ?: return false
        if (now - free.lastGestureAt < delayMillis) return false
        mode = Mode.Following
        return true
    }

    companion object {
        const val RECENTER_DELAY_MS = 5_000L
    }
}
