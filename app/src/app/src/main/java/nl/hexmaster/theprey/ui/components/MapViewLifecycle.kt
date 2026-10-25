package nl.hexmaster.theprey.ui.components

import androidx.compose.runtime.Composable
import androidx.compose.runtime.DisposableEffect
import androidx.lifecycle.Lifecycle
import androidx.lifecycle.LifecycleEventObserver
import androidx.lifecycle.compose.LocalLifecycleOwner
import org.maplibre.android.maps.MapView
import java.util.Locale

/**
 * Forwards the screen's lifecycle to [mapView] and destroys it when it leaves composition; a
 * MapLibre `MapView` leaks or crashes when an event is missed.
 */
@Composable
fun MapViewLifecycle(mapView: MapView) {
    val lifecycle = LocalLifecycleOwner.current.lifecycle
    DisposableEffect(lifecycle, mapView) {
        val observer = LifecycleEventObserver { _, event ->
            when (event) {
                Lifecycle.Event.ON_CREATE -> mapView.onCreate(null)
                Lifecycle.Event.ON_START -> mapView.onStart()
                Lifecycle.Event.ON_RESUME -> mapView.onResume()
                Lifecycle.Event.ON_PAUSE -> mapView.onPause()
                Lifecycle.Event.ON_STOP -> mapView.onStop()
                else -> Unit // ON_DESTROY: handled once, in onDispose.
            }
        }
        lifecycle.addObserver(observer)
        onDispose {
            lifecycle.removeObserver(observer)
            val state = lifecycle.currentState
            if (state.isAtLeast(Lifecycle.State.RESUMED)) mapView.onPause()
            if (state.isAtLeast(Lifecycle.State.STARTED)) mapView.onStop()
            mapView.onDestroy()
        }
    }
}

/** A MapLibre style with only a background in [backgroundArgb]: the offline / provider-down fallback. */
fun fallbackMapStyle(backgroundArgb: Int): String {
    val hex = String.format(Locale.ROOT, "#%06X", backgroundArgb and 0xFFFFFF)
    return """{"version":8,"sources":{},"layers":[{"id":"background","type":"background","paint":{"background-color":"$hex"}}]}"""
}
