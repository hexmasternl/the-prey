package nl.hexmaster.theprey

import android.app.Application
import kotlinx.coroutines.launch

class ThePreyApplication : Application() {

    lateinit var container: AppContainer
        private set

    override fun onCreate() {
        super.onCreate()
        container = AppContainer(this)
        // Decide the signed-in state once per process; screens observe the result.
        container.appScope.launch { container.sessionManager.restore() }
    }
}
