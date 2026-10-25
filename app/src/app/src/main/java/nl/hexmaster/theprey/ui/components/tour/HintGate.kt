package nl.hexmaster.theprey.ui.components.tour

import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.NonCancellable
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import nl.hexmaster.theprey.data.hints.Hint
import nl.hexmaster.theprey.data.hints.HintsStore

/**
 * Whether [hint]'s tour shows: read once from [store] when created, shown only when the screen is
 * ready and the tour is not seen. [finish] hides it at once and marks it seen in the background, so
 * a slow or failing write never keeps the overlay up. Leaving the screen without [finish] leaves
 * the tour not seen.
 */
class HintGate(private val store: HintsStore, private val hint: Hint, private val scope: CoroutineScope) {

    /** Null until the stored flag is read. */
    var seen: Boolean? by mutableStateOf(null)
        private set

    private var finished by mutableStateOf(false)

    init {
        scope.launch {
            seen = try {
                store.seen(hint).first()
            } catch (e: CancellationException) {
                throw e
            } catch (_: Exception) {
                // Storage problems never block the screen: offer the tour.
                false
            }
        }
    }

    fun shows(ready: Boolean): Boolean = ready && seen == false && !finished

    fun finish() {
        if (finished) return
        finished = true
        scope.launch {
            // Done or Skip right before leaving the screen still gets stored.
            withContext(NonCancellable) {
                try {
                    store.markSeen(hint)
                } catch (_: Exception) {
                    // At worst the tour shows again later.
                }
            }
        }
    }
}

/** What a screen needs to show a tour: whether it shows now, and how to close it. */
class HintHandle(val show: Boolean, val finish: () -> Unit)

/** The [HintGate] of [hint] for this screen; never shows without a [store]. */
@Composable
fun rememberHint(store: HintsStore?, hint: Hint, ready: Boolean): HintHandle {
    val scope = rememberCoroutineScope()
    val gate = remember(store, hint) { store?.let { HintGate(it, hint, scope) } }
    return HintHandle(show = gate?.shows(ready) == true, finish = { gate?.finish() })
}
