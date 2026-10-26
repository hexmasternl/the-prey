package nl.hexmaster.theprey.ui.update

import androidx.compose.foundation.layout.Box
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.ui.Modifier
import androidx.compose.ui.semantics.clearAndSetSemantics
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.navigation.NavHostController
import androidx.navigation.compose.currentBackStackEntryAsState
import nl.hexmaster.theprey.data.version.AppVersionGate
import nl.hexmaster.theprey.data.version.GateState
import nl.hexmaster.theprey.ui.navigation.Routes

/**
 * Routes of an unfinished game. `GET /games/active` only reports started games, so the lobby is
 * exempted here by route; the game route also keeps the result of a game that just ended visible.
 */
private val GAME_ROUTES = setOf(Routes.LOBBY, Routes.GAME)

/** Whether the update message covers the screen at [route]. */
fun showsUpdateScreen(state: GateState, route: String?): Boolean =
    state == GateState.Blocked && route !in GAME_ROUTES

/** Whether navigating from [previous] to [current] left the lobby or game for another screen. */
fun leftGame(previous: String?, current: String?): Boolean =
    previous in GAME_ROUTES && current !in GAME_ROUTES

/**
 * Draws [content] (the nav host driven by [navController]) and, while [gate] is blocked outside a
 * lobby or game, the [UpdateRequiredScreen] on top of it. Leaving a lobby or game while deferred
 * checks again right away, so the block follows the end of the game.
 */
@Composable
fun UpdateGate(
    gate: AppVersionGate,
    navController: NavHostController,
    modifier: Modifier = Modifier,
    content: @Composable () -> Unit,
) {
    val state by gate.state.collectAsStateWithLifecycle()
    val entry by navController.currentBackStackEntryAsState()
    val route = entry?.destination?.route

    val previousRoute = remember { mutableStateOf<String?>(null) }
    LaunchedEffect(route) {
        val left = leftGame(previousRoute.value, route)
        previousRoute.value = route
        if (left && gate.state.value == GateState.Deferred) gate.check()
    }

    val blocked = showsUpdateScreen(state, route)
    Box(modifier) {
        // While blocked, the screens underneath are hidden from accessibility services too.
        Box(if (blocked) Modifier.clearAndSetSemantics {} else Modifier) { content() }
        if (blocked) UpdateRequiredScreen()
    }
}
