package nl.hexmaster.theprey.ui.components.tour

import androidx.annotation.StringRes

/** How a tour was closed. Both mark it seen. */
enum class TourOutcome { Completed, Skipped }

/**
 * One step of a tour: the control to spotlight ([targetKey], see [tourTarget]; null for a centred
 * card), a title and a body. [bodyArgs] fill the body's format arguments.
 */
data class TourStep(
    val targetKey: String?,
    @param:StringRes val title: Int,
    @param:StringRes val body: Int,
    val bodyArgs: List<Any> = emptyList(),
)

/** Where a tour of [stepCount] steps is: step [index], zero-based. */
data class TourState(val stepCount: Int, val index: Int = 0) {
    init {
        require(stepCount > 0) { "A tour needs at least one step" }
        require(index in 0 until stepCount) { "Step $index is outside 0 until $stepCount" }
    }

    val isFirst: Boolean get() = index == 0
    val isLast: Boolean get() = index == stepCount - 1

    /** The one-based step number for the counter ("2 / 6"). */
    val number: Int get() = index + 1

    /** The next step; stays on the last one (Done closes the tour instead). */
    fun next(): TourState = if (isLast) this else copy(index = index + 1)

    fun back(): TourState = if (isFirst) this else copy(index = index - 1)
}
