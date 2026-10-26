package nl.hexmaster.theprey

import android.content.Intent
import android.os.Build
import android.os.Bundle
import android.os.SystemClock
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import androidx.appcompat.app.AppCompatActivity
import androidx.compose.animation.Crossfade
import androidx.compose.animation.core.tween
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.core.splashscreen.SplashScreen.Companion.installSplashScreen
import androidx.lifecycle.Lifecycle
import androidx.lifecycle.lifecycleScope
import androidx.lifecycle.repeatOnLifecycle
import androidx.navigation.compose.rememberNavController
import kotlinx.coroutines.delay
import kotlinx.coroutines.launch
import nl.hexmaster.theprey.auth.SessionState
import nl.hexmaster.theprey.deeplink.parseJoinLink
import nl.hexmaster.theprey.tracking.TrackingNotifications
import nl.hexmaster.theprey.ui.navigation.ThePreyNavHost
import nl.hexmaster.theprey.ui.splash.LaunchSequence
import nl.hexmaster.theprey.ui.splash.LaunchSequenceGate
import nl.hexmaster.theprey.ui.splash.LaunchTimeline
import nl.hexmaster.theprey.ui.theme.ThePreyTheme
import nl.hexmaster.theprey.ui.tracking.TrackingRequest
import nl.hexmaster.theprey.ui.update.UpdateGate
import kotlin.time.Duration.Companion.minutes

/** AppCompatActivity (not ComponentActivity) so the per-app language from Settings works below Android 13. */
class MainActivity : AppCompatActivity() {

    /** True once the system splash is gone; the launch sequence's clock starts only then. */
    private var splashReleased by mutableStateOf(false)

    /** A tap on the "resume tracking" notification; app start itself is covered by TrackingAutoStart. */
    private var trackingRequest by mutableStateOf<TrackingRequest?>(null)

    override fun onCreate(savedInstanceState: Bundle?) {
        val container = (application as ThePreyApplication).container
        val session = container.sessionManager
        val coldLaunch = savedInstanceState == null && !LaunchSequenceGate.hasPlayed
        // On a cold launch with an animated splash icon (API 31+), let the icon animation finish so the
        // launch sequence picks up from its last frame instead of a half-drawn ring.
        val iconAnimationEnd = if (coldLaunch && Build.VERSION.SDK_INT >= Build.VERSION_CODES.S) {
            SystemClock.uptimeMillis() + SPLASH_ICON_ANIMATION_MS
        } else {
            0L
        }
        splashReleased = savedInstanceState != null
        // A recreated activity already handled the intent that opened it.
        if (savedInstanceState == null) {
            onResumeTrackingIntent(intent)
            onInviteIntent(intent)
        }
        // Hold the system splash until the stored session is checked, so a signed-in
        // player never sees the landing page flash by.
        installSplashScreen().setKeepOnScreenCondition {
            val hold = session.state.value == SessionState.Unknown || SystemClock.uptimeMillis() < iconAnimationEnd
            if (!hold) splashReleased = true
            hold
        }
        super.onCreate(savedInstanceState)
        enableEdgeToEdge()
        setContent {
            ThePreyTheme {
                // Cold start only: the launch sequence takes over from the system splash, then fades
                // into whatever the session routes to (landing page or the signed-in area).
                var showLaunch by remember { mutableStateOf(!LaunchSequenceGate.hasPlayed) }
                Crossfade(
                    targetState = showLaunch,
                    animationSpec = tween(LaunchTimeline.CROSSFADE_MS),
                    label = "launch",
                ) { launching ->
                    if (launching) {
                        LaunchSequence(
                            started = splashReleased,
                            onFinished = {
                                LaunchSequenceGate.markPlayed()
                                showLaunch = false
                            },
                        )
                    } else {
                        val navController = rememberNavController()
                        UpdateGate(gate = container.appVersionGate, navController = navController) {
                            ThePreyNavHost(
                                session = session,
                                locationSource = container.locationSource,
                                locationConsentStore = container.locationConsentStore,
                                gameRepository = container.gameRepository,
                                playFieldRepository = container.playFieldRepository,
                                profileStore = container.profileStore,
                                gameSessions = container.gameStateService,
                                headingSource = container.headingSource,
                                profileSettings = session,
                                appLanguage = container.appLanguage,
                                pendingInviteStore = container.pendingInviteStore,
                                hintsStore = container.hintsStore,
                                displayPreferencesStore = container.displayPreferencesStore,
                                trackingStarter = container.trackingStarter,
                                trackingRequest = trackingRequest,
                                navController = navController,
                            )
                        }
                    }
                }
            }
        }
        // The version check: on start, on every return to the foreground and every 10 minutes while visible.
        lifecycleScope.launch {
            repeatOnLifecycle(Lifecycle.State.STARTED) {
                while (true) {
                    container.appVersionGate.check()
                    delay(VERSION_CHECK_INTERVAL)
                }
            }
        }
    }

    override fun onNewIntent(intent: Intent) {
        super.onNewIntent(intent)
        setIntent(intent)
        onResumeTrackingIntent(intent)
        onInviteIntent(intent)
    }

    /** Keeps a tapped invitation link until the player is signed in; the nav host then joins it. */
    private fun onInviteIntent(intent: Intent?) {
        val invite = intent?.dataString?.let(::parseJoinLink) ?: return
        val store = (application as ThePreyApplication).container.pendingInviteStore
        val receivedAt = System.currentTimeMillis()
        lifecycleScope.launch { store.save(invite, receivedAt) }
    }

    private fun onResumeTrackingIntent(intent: Intent?) {
        val gameId = intent?.getStringExtra(TrackingNotifications.EXTRA_RESUME_TRACKING) ?: return
        TrackingNotifications.cancelResume(this)
        trackingRequest = TrackingRequest(gameId, SystemClock.uptimeMillis())
    }

    private companion object {
        /** Matches windowSplashScreenAnimationDuration in Theme.ThePrey.Starting. */
        const val SPLASH_ICON_ANIMATION_MS = 800L

        val VERSION_CHECK_INTERVAL = 10.minutes
    }
}
