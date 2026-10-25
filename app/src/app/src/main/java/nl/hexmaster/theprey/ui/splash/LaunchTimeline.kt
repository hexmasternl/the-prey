package nl.hexmaster.theprey.ui.splash

import kotlin.math.PI
import kotlin.math.cos
import kotlin.math.pow
import kotlin.math.sin

/** One expanding radar ping: [radius] as a fraction of the sweep radius, faded by [alpha]. */
data class Ping(val radius: Float, val alpha: Float)

/**
 * Everything the launch sequence draws at one instant. All fractions are 0..1. The composable
 * only renders a frame; all timing lives in [LaunchTimeline].
 */
data class LaunchFrame(
    /** Field grid opacity. */
    val gridAlpha: Float,
    /** How far the corner brackets have extended. */
    val bracketProgress: Float,
    /** Radar sweep leading-edge angle in degrees, clockwise from 12 o'clock. */
    val sweepAngle: Float,
    val sweepAlpha: Float,
    val pings: List<Ping>,
    /** "Target acquired" flash on the prey head. */
    val headFlash: Float,
    /** 0 = hand-off pose (centred, same place as the system splash icon), 1 = raised for the title. */
    val markLift: Float,
    val markScale: Float,
    /** Number of title characters revealed. */
    val titleChars: Int,
    val cursorVisible: Boolean,
    val taglineAlpha: Float,
    /** Remaining slide-in distance of the tagline (1 = start, 0 = settled). */
    val taglineSlide: Float,
    /** Index into the status messages, or -1 before the first one. */
    val statusIndex: Int,
    val readoutAlpha: Float,
    val isFinished: Boolean,
)

/**
 * The launch sequence's timeline: a pure mapping from elapsed time to a [LaunchFrame], so the
 * whole sequence (and its 3-second cap) is testable without a device. Tune the constants here.
 */
object LaunchTimeline {
    /** Total length of the sequence; the spec caps it at 3000 ms. */
    const val TOTAL_MS = 2400L

    /** Crossfade from the sequence to the first screen. Spec: at most 400 ms. */
    const val CROSSFADE_MS = 300

    /** How long the static final frame is shown when animations are turned off. */
    const val REDUCED_MOTION_HOLD_MS = 1000L

    /** Number of entries in the `launch_status_messages` string array. */
    const val STATUS_COUNT = 4

    /** Final scale of the mark after it makes room for the title. */
    const val MARK_END_SCALE = 0.8f

    // Hand-off: the mark holds still while grid and brackets fade in.
    private const val HOLD_END = 300L
    private const val CHROME_IN_MS = 300L

    // Radar sweep and pings.
    private const val SWEEP_START = 200L
    private const val SWEEP_PERIOD_MS = 1700f
    private const val SWEEP_FADE_IN_MS = 200f
    private const val PING_INTERVAL = 600L
    private const val PING_LIFE_MS = 1200f

    // Mark lift and head flash.
    private const val LIFT_START = 450L
    private const val LIFT_END = 650L
    private const val FLASH_START = 450L
    private const val FLASH_MS = 350f

    // Typed title.
    private const val TITLE_START = 700L
    private const val CHAR_MS = 75L
    private const val CURSOR_BLINK_MS = 150L
    private const val CURSOR_LINGER_MS = 300L

    // Tagline and status line.
    private const val TAGLINE_START = 1350L
    private const val TAGLINE_MS = 400f
    private const val STATUS_START = 300L
    private const val STATUS_STEP = 500L

    /** Default title length: "THE PREY". */
    const val DEFAULT_TITLE_LENGTH = 8

    fun frameAt(elapsedMs: Long, titleLength: Int = DEFAULT_TITLE_LENGTH): LaunchFrame {
        val t = elapsedMs.coerceAtLeast(0L)
        if (t >= TOTAL_MS) return finalFrame(titleLength)

        val chrome = progress(t, 0L, CHROME_IN_MS.toFloat())
        val sweepElapsed = (t - SWEEP_START).coerceAtLeast(0L)
        val sweepOn = t >= SWEEP_START
        val sweepAngle = if (sweepOn) (sweepElapsed / SWEEP_PERIOD_MS * 360f) % 360f else 0f
        val sweepAlpha = if (sweepOn) (sweepElapsed / SWEEP_FADE_IN_MS).coerceIn(0f, 1f) else 0f

        val pings = buildList {
            if (sweepOn) {
                var emitted = SWEEP_START
                while (emitted <= t) {
                    val age = (t - emitted) / PING_LIFE_MS
                    if (age < 1f) add(Ping(radius = easeOut(age), alpha = 1f - age))
                    emitted += PING_INTERVAL
                }
            }
        }

        val lift = easeInOut(progress(t, LIFT_START, (LIFT_END - LIFT_START).toFloat()))
        val flashT = (t - FLASH_START) / FLASH_MS
        // One short blink up and back down: a "lock" rather than a lingering glow.
        val headFlash = if (flashT in 0f..1f) sin(flashT * PI).toFloat().pow(0.6f) else 0f

        val titleChars = if (t < TITLE_START) 0 else (((t - TITLE_START) / CHAR_MS).toInt() + 1).coerceAtMost(titleLength)
        val typingEnd = TITLE_START + titleLength * CHAR_MS
        val cursorVisible = t >= TITLE_START && t < typingEnd + CURSOR_LINGER_MS &&
            ((t - TITLE_START) / CURSOR_BLINK_MS) % 2L == 0L

        val tagline = easeOut(progress(t, TAGLINE_START, TAGLINE_MS))
        val statusIndex = if (t < STATUS_START) -1 else ((t - STATUS_START) / STATUS_STEP).toInt().coerceAtMost(STATUS_COUNT - 1)

        return LaunchFrame(
            gridAlpha = chrome,
            bracketProgress = easeOut(chrome),
            sweepAngle = sweepAngle,
            sweepAlpha = sweepAlpha,
            pings = pings,
            headFlash = headFlash,
            markLift = lift,
            markScale = 1f + (MARK_END_SCALE - 1f) * lift,
            titleChars = titleChars,
            cursorVisible = cursorVisible,
            taglineAlpha = tagline,
            taglineSlide = 1f - tagline,
            statusIndex = statusIndex,
            readoutAlpha = chrome,
            isFinished = false,
        )
    }

    /** The settled end state, without any motion: also what reduced motion shows. */
    fun finalFrame(titleLength: Int = DEFAULT_TITLE_LENGTH): LaunchFrame = LaunchFrame(
        gridAlpha = 1f,
        bracketProgress = 1f,
        sweepAngle = 0f,
        sweepAlpha = 0f,
        pings = emptyList(),
        headFlash = 0f,
        markLift = 1f,
        markScale = MARK_END_SCALE,
        titleChars = titleLength,
        cursorVisible = false,
        taglineAlpha = 1f,
        taglineSlide = 0f,
        statusIndex = STATUS_COUNT - 1,
        readoutAlpha = 1f,
        isFinished = true,
    )

    private fun progress(t: Long, start: Long, durationMs: Float): Float =
        ((t - start) / durationMs).coerceIn(0f, 1f)

    // Design-system ease-out cubic-bezier(.2,.8,.2,1) is close to a cubic ease-out.
    private fun easeOut(x: Float): Float = 1f - (1f - x).pow(3)

    private fun easeInOut(x: Float): Float = (0.5f - cos(x * PI).toFloat() / 2f)
}
