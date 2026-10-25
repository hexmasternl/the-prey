package nl.hexmaster.theprey.ui.splash

/**
 * Remembers, for the lifetime of the process, whether the launch sequence has played. A fresh
 * process (true cold start) starts with [hasPlayed] false; rotation, theme changes, returning from
 * the background and relaunching the activity while the process lives all keep it true.
 */
object LaunchSequenceGate {
    @Volatile
    var hasPlayed: Boolean = false
        private set

    fun markPlayed() {
        hasPlayed = true
    }
}
