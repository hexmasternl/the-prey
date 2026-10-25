package nl.hexmaster.theprey

import android.app.Application
import kotlinx.coroutines.launch
import org.maplibre.android.MapLibre

class ThePreyApplication : Application() {

    lateinit var container: AppContainer
        private set

    override fun onCreate() {
        super.onCreate()
        // Once per process, before any MapView exists (playfield area map; keyless OpenFreeMap tiles).
        MapLibre.getInstance(this)
        container = AppContainer(this)
        // Decide the signed-in state once per process; screens observe the result.
        container.appScope.launch { container.sessionManager.restore() }
    }
}
