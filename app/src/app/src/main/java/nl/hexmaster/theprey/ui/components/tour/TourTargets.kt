package nl.hexmaster.theprey.ui.components.tour

import androidx.compose.runtime.DisposableEffect
import androidx.compose.runtime.Stable
import androidx.compose.runtime.mutableStateMapOf
import androidx.compose.runtime.staticCompositionLocalOf
import androidx.compose.ui.Modifier
import androidx.compose.ui.composed
import androidx.compose.ui.geometry.Rect
import androidx.compose.ui.layout.boundsInRoot
import androidx.compose.ui.layout.onGloballyPositioned

/**
 * The controls a tour can spotlight: key → bounds in root coordinates. Snapshot state, so the
 * overlay follows a control that moves (e.g. after a rotation).
 */
@Stable
class TourTargets {
    private val bounds = mutableStateMapOf<String, Rect>()

    operator fun get(key: String): Rect? = bounds[key]

    fun update(key: String, rect: Rect) {
        if (bounds[key] != rect) bounds[key] = rect
    }

    fun remove(key: String) {
        bounds.remove(key)
    }
}

/** The registry of the screen that shows a tour; null (no tour) by default. */
val LocalTourTargets = staticCompositionLocalOf<TourTargets?> { null }

/**
 * Registers this control's bounds under [key] for a tour to spotlight. Does nothing without a
 * [LocalTourTargets] provider, so previews and screens without a tour are unaffected.
 */
fun Modifier.tourTarget(key: String): Modifier = composed {
    val targets = LocalTourTargets.current
    if (targets == null) {
        Modifier
    } else {
        DisposableEffect(targets, key) { onDispose { targets.remove(key) } }
        Modifier.onGloballyPositioned { targets.update(key, it.boundsInRoot()) }
    }
}
