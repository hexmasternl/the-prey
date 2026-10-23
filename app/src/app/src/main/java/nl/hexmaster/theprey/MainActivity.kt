package nl.hexmaster.theprey

import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import androidx.core.splashscreen.SplashScreen.Companion.installSplashScreen
import nl.hexmaster.theprey.auth.SessionState
import nl.hexmaster.theprey.ui.navigation.ThePreyNavHost
import nl.hexmaster.theprey.ui.theme.ThePreyTheme

class MainActivity : ComponentActivity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        val container = (application as ThePreyApplication).container
        val session = container.sessionManager
        // Hold the system splash until the stored session is checked, so a signed-in
        // player never sees the landing page flash by.
        installSplashScreen().setKeepOnScreenCondition { session.state.value == SessionState.Unknown }
        super.onCreate(savedInstanceState)
        enableEdgeToEdge()
        setContent {
            ThePreyTheme {
                ThePreyNavHost(session, container.locationSource)
            }
        }
    }
}
