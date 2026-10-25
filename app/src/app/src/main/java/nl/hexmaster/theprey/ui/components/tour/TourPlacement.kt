package nl.hexmaster.theprey.ui.components.tour

import androidx.compose.ui.geometry.Rect

/** Where the step card sits relative to the spotlighted control. */
enum class CardPosition { Above, Below, Centered }

/** Pure geometry of a tour step, in the overlay's own pixel coordinates. */
object TourPlacement {

    /** The cut-out around [target]: grown by [padding] on every side, kept inside [screen]. */
    fun spotlight(target: Rect, padding: Float, screen: Rect): Rect = target.inflate(padding).intersect(screen)

    /** Below the target when there is at least as much room there as above it; centred without a target. */
    fun position(target: Rect?, safe: Rect): CardPosition {
        if (target == null) return CardPosition.Centered
        val above = target.top - safe.top
        val below = safe.bottom - target.bottom
        return if (below >= above) CardPosition.Below else CardPosition.Above
    }

    /**
     * The card's top edge for a card of [cardHeight], [gap] away from the spotlight, clamped so the
     * card stays inside [safe] (top-aligned when it is taller than the safe area).
     */
    fun cardTop(spotlight: Rect?, safe: Rect, cardHeight: Float, gap: Float): Float {
        val wanted = when (position(spotlight, safe)) {
            CardPosition.Centered -> safe.center.y - cardHeight / 2
            CardPosition.Below -> spotlight!!.bottom + gap
            CardPosition.Above -> spotlight!!.top - gap - cardHeight
        }
        val max = (safe.bottom - cardHeight).coerceAtLeast(safe.top)
        return wanted.coerceIn(safe.top, max)
    }
}
