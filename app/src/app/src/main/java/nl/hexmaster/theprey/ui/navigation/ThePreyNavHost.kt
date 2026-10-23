package nl.hexmaster.theprey.ui.navigation

import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.remember
import androidx.compose.ui.Modifier
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.lifecycle.viewmodel.compose.viewModel
import androidx.navigation.NavDestination.Companion.hierarchy
import androidx.navigation.compose.NavHost
import androidx.navigation.compose.composable
import androidx.navigation.compose.navigation
import androidx.navigation.compose.rememberNavController
import nl.hexmaster.theprey.BuildConfig
import nl.hexmaster.theprey.R
import nl.hexmaster.theprey.auth.SessionController
import nl.hexmaster.theprey.auth.SessionState
import nl.hexmaster.theprey.data.location.LocationSource
import nl.hexmaster.theprey.ui.landing.LandingScreen
import nl.hexmaster.theprey.ui.landing.LandingViewModel
import nl.hexmaster.theprey.ui.mainmenu.MainMenuScreen
import nl.hexmaster.theprey.ui.mainmenu.MainMenuViewModel
import nl.hexmaster.theprey.ui.placeholder.PlaceholderScreen

object Routes {
    const val LANDING = "landing"

    /** The signed-in area: the main menu and everything opened from it. */
    const val SIGNED_IN = "signed_in"
    const val MAIN_MENU = "main_menu"
    const val PLAY = "play"
    const val PLAYFIELDS = "playfields"
    const val SETTINGS = "settings"
}

/** The destination a session state belongs on, or null while the stored session is still being checked. */
fun routeFor(state: SessionState): String? = when (state) {
    SessionState.Unknown -> null
    is SessionState.SignedOut -> Routes.LANDING
    is SessionState.SignedIn -> Routes.SIGNED_IN
}

/**
 * Root navigation, driven by the session: signed-in players are always in the signed-in area
 * (main menu and its sub-screens), everyone else on the landing page. Each switch between the two
 * clears the back stack, so Back leaves the app.
 */
@Composable
fun ThePreyNavHost(session: SessionController, locationSource: LocationSource, modifier: Modifier = Modifier) {
    val state by session.state.collectAsStateWithLifecycle()
    // Nothing to show yet: the system splash stays up until the session is known.
    val target = routeFor(state) ?: return

    val navController = rememberNavController()
    val startDestination = remember { target }

    NavHost(navController = navController, startDestination = startDestination, modifier = modifier) {
        composable(Routes.LANDING) {
            LandingScreen(viewModel { LandingViewModel(session) })
        }
        navigation(route = Routes.SIGNED_IN, startDestination = Routes.MAIN_MENU) {
            composable(Routes.MAIN_MENU) {
                MainMenuScreen(
                    viewModel = viewModel { MainMenuViewModel(session, locationSource, BuildConfig.VERSION_NAME) },
                    onPlayNow = { navController.navigate(Routes.PLAY) },
                    onPlayfields = { navController.navigate(Routes.PLAYFIELDS) },
                    onSettings = { navController.navigate(Routes.SETTINGS) },
                )
            }
            composable(Routes.PLAY) {
                PlaceholderScreen(R.string.main_menu_play_now, onBack = { navController.popBackStack() })
            }
            composable(Routes.PLAYFIELDS) {
                PlaceholderScreen(R.string.main_menu_playfields, onBack = { navController.popBackStack() })
            }
            composable(Routes.SETTINGS) {
                PlaceholderScreen(R.string.main_menu_settings, onBack = { navController.popBackStack() })
            }
        }
    }

    LaunchedEffect(target) {
        // Already somewhere inside the target area (e.g. on Playfields after a rotation): stay there.
        val current = navController.currentDestination
        if (current?.hierarchy?.any { it.route == target } != true) {
            navController.navigate(target) {
                popUpTo(navController.graph.id) { inclusive = true }
                launchSingleTop = true
            }
        }
    }
}
