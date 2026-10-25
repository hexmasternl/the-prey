package nl.hexmaster.theprey.ui.navigation

import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.remember
import androidx.compose.ui.Modifier
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.lifecycle.createSavedStateHandle
import androidx.lifecycle.viewmodel.compose.viewModel
import androidx.navigation.NavDestination.Companion.hierarchy
import androidx.navigation.NavBackStackEntry
import androidx.navigation.NavType
import androidx.navigation.compose.NavHost
import androidx.navigation.compose.composable
import androidx.navigation.compose.currentBackStackEntryAsState
import androidx.navigation.compose.navigation
import androidx.navigation.compose.rememberNavController
import androidx.navigation.navArgument
import nl.hexmaster.theprey.BuildConfig
import nl.hexmaster.theprey.R
import nl.hexmaster.theprey.auth.ProfileSettings
import nl.hexmaster.theprey.auth.SessionController
import nl.hexmaster.theprey.auth.SessionState
import nl.hexmaster.theprey.data.game.GameRepository
import nl.hexmaster.theprey.data.language.AppLanguage
import nl.hexmaster.theprey.data.location.LocationConsentStore
import nl.hexmaster.theprey.data.location.LocationSource
import nl.hexmaster.theprey.data.playfield.PlayFieldRepository
import nl.hexmaster.theprey.tracking.TrackingStarter
import nl.hexmaster.theprey.ui.consent.LocationConsentHost
import nl.hexmaster.theprey.ui.landing.LandingScreen
import nl.hexmaster.theprey.ui.landing.LandingViewModel
import nl.hexmaster.theprey.ui.mainmenu.MainMenuScreen
import nl.hexmaster.theprey.ui.mainmenu.MainMenuViewModel
import nl.hexmaster.theprey.ui.placeholder.PlaceholderScreen
import nl.hexmaster.theprey.ui.playfields.PlayFieldsScreen
import nl.hexmaster.theprey.ui.playfields.PlayFieldsViewModel
import nl.hexmaster.theprey.ui.playfields.area.PlayFieldAreaScreen
import nl.hexmaster.theprey.ui.playfields.area.PlayFieldAreaViewModel
import nl.hexmaster.theprey.ui.playfields.details.PlayFieldDetailsScreen
import nl.hexmaster.theprey.ui.playfields.details.PlayFieldDetailsViewModel
import nl.hexmaster.theprey.ui.playfields.select.PlayFieldSelectResult
import nl.hexmaster.theprey.ui.playfields.select.PlayFieldSelectScreen
import nl.hexmaster.theprey.ui.playfields.select.PlayFieldSelectViewModel
import nl.hexmaster.theprey.ui.settings.SettingsScreen
import nl.hexmaster.theprey.ui.settings.SettingsViewModel
import nl.hexmaster.theprey.ui.tracking.TrackingAutoStart
import nl.hexmaster.theprey.ui.tracking.TrackingRequest

object Routes {
    const val LANDING = "landing"

    /** The signed-in area: the main menu and everything opened from it. */
    const val SIGNED_IN = "signed_in"
    const val MAIN_MENU = "main_menu"
    const val PLAY = "play"

    /** A running game the player takes part in; opened by Resume game. */
    const val GAME = "game/{gameId}"
    const val GAME_ID = "gameId"

    fun game(gameId: String) = "game/$gameId"
    const val PLAYFIELDS = "playfields"

    /** Creates a new playfield; opened by the create button on the Private tab. */
    const val PLAYFIELD_EDITOR = "playfield_editor"

    /**
     * One playfield, opened from the list: a nested graph holding its details (start) and its area
     * map, which share the details' unsaved draft. Leaving the graph discards the draft.
     */
    const val PLAYFIELD = "playfield/{playFieldId}?editable={editable}"
    const val PLAYFIELD_ID = "playFieldId"
    const val PLAYFIELD_EDITABLE = "editable"

    /** Private-tab playfields open [editable]; Public-tab results read-only. */
    fun playField(id: String, editable: Boolean) = "playfield/$id?editable=$editable"
    const val PLAYFIELD_DETAILS = "playfield_details"
    const val PLAYFIELD_AREA = "playfield_area"
    const val SETTINGS = "settings"

    /**
     * Picks one playfield and hands it back to the destination that opened it. Cancel and Back
     * return nothing. A caller consumes the result from its own back-stack entry:
     * `PlayFieldSelectResult.consume(entry.savedStateHandle).collect { selection -> ... }`.
     */
    const val PLAYFIELD_SELECT = "playfield_select"
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
 * clears the back stack, so Back leaves the app. Signed-in players get the location tracking
 * disclosure on top (see [LocationConsentHost]): once by itself on the main menu, and before game actions.
 * While signed in, in-game location tracking is resumed for a running game (see [TrackingAutoStart]);
 * [trackingRequest] carries a tap on the resume notification.
 */
@Composable
fun ThePreyNavHost(
    session: SessionController,
    locationSource: LocationSource,
    locationConsentStore: LocationConsentStore,
    gameRepository: GameRepository,
    playFieldRepository: PlayFieldRepository,
    profileSettings: ProfileSettings,
    appLanguage: AppLanguage,
    modifier: Modifier = Modifier,
    trackingStarter: TrackingStarter? = null,
    trackingRequest: TrackingRequest? = null,
) {
    val state by session.state.collectAsStateWithLifecycle()
    // Nothing to show yet: the system splash stays up until the session is known.
    val target = routeFor(state) ?: return

    val navController = rememberNavController()
    val startDestination = remember { target }

    val currentEntry by navController.currentBackStackEntryAsState()
    LocationConsentHost(
        store = locationConsentStore,
        active = state is SessionState.SignedIn,
        // The disclosure appears by itself only here, and only once per device.
        onMainMenu = currentEntry?.destination?.route == Routes.MAIN_MENU,
    ) {
        if (trackingStarter != null) {
            TrackingAutoStart(
                starter = trackingStarter,
                signedInUserId = (state as? SessionState.SignedIn)?.profile?.userId,
                request = trackingRequest,
            )
        }
        NavHost(navController = navController, startDestination = startDestination, modifier = modifier) {
            composable(Routes.LANDING) {
                LandingScreen(viewModel { LandingViewModel(session) })
            }
            navigation(route = Routes.SIGNED_IN, startDestination = Routes.MAIN_MENU) {
                composable(Routes.MAIN_MENU) {
                    MainMenuScreen(
                        viewModel = viewModel { MainMenuViewModel(session, locationSource, gameRepository, BuildConfig.VERSION_NAME) },
                        onPlayNow = { navController.navigate(Routes.PLAY) },
                        onResumeGame = { gameId -> navController.navigate(Routes.game(gameId)) },
                        onPlayfields = { navController.navigate(Routes.PLAYFIELDS) },
                        onSettings = { navController.navigate(Routes.SETTINGS) },
                    )
                }
                composable(Routes.PLAY) {
                    PlaceholderScreen(R.string.main_menu_play_now, onBack = { navController.popBackStack() })
                }
                // Placeholder until the game screen exists; it will read Routes.GAME_ID from the entry.
                composable(Routes.GAME, arguments = listOf(navArgument(Routes.GAME_ID) { type = NavType.StringType })) {
                    PlaceholderScreen(R.string.main_menu_resume_game, onBack = { navController.popBackStack() })
                }
                // Scoped to this entry: leaving to the menu discards it, so the next open starts on Private.
                composable(Routes.PLAYFIELDS) {
                    PlayFieldsScreen(
                        viewModel = viewModel { PlayFieldsViewModel(playFieldRepository, createSavedStateHandle()) },
                        onBack = { navController.popBackStack() },
                        onCreate = { navController.navigate(Routes.PLAYFIELD_EDITOR) },
                        onOpen = { id, editable -> navController.navigate(Routes.playField(id, editable)) },
                    )
                }
                navigation(
                    route = Routes.PLAYFIELD,
                    startDestination = Routes.PLAYFIELD_DETAILS,
                    arguments = listOf(
                        navArgument(Routes.PLAYFIELD_ID) { type = NavType.StringType },
                        navArgument(Routes.PLAYFIELD_EDITABLE) {
                            type = NavType.BoolType
                            defaultValue = false
                        },
                    ),
                ) {
                    composable(Routes.PLAYFIELD_DETAILS) { entry ->
                        val graphEntry = remember(entry) { navController.getBackStackEntry(Routes.PLAYFIELD) }
                        PlayFieldDetailsScreen(
                            viewModel = playFieldDetailsViewModel(graphEntry, playFieldRepository),
                            // Save, Cancel and Back leave the whole graph, which discards the draft.
                            onClose = {
                                if (navController.currentBackStackEntry == entry) {
                                    navController.popBackStack(Routes.PLAYFIELD, inclusive = true)
                                }
                            },
                            onOpenArea = {
                                if (navController.currentBackStackEntry == entry) navController.navigate(Routes.PLAYFIELD_AREA)
                            },
                        )
                    }
                    composable(Routes.PLAYFIELD_AREA) { entry ->
                        val graphEntry = remember(entry) { navController.getBackStackEntry(Routes.PLAYFIELD) }
                        val details = playFieldDetailsViewModel(graphEntry, playFieldRepository)
                        PlayFieldAreaScreen(
                            viewModel = viewModel {
                                val draft = details.uiState.value
                                PlayFieldAreaViewModel(draft.points, draft.editable, createSavedStateHandle(), details::applyArea)
                            },
                            locationSource = locationSource,
                            onClose = { if (navController.currentBackStackEntry == entry) navController.popBackStack() },
                        )
                    }
                }
                // Placeholder until the playfield editor exists.
                composable(Routes.PLAYFIELD_EDITOR) {
                    PlaceholderScreen(R.string.playfield_editor_title, onBack = { navController.popBackStack() })
                }
                // Scoped to this entry: every open starts on Private with nothing selected.
                composable(Routes.PLAYFIELD_SELECT) {
                    PlayFieldSelectScreen(
                        viewModel = viewModel { PlayFieldSelectViewModel(playFieldRepository, createSavedStateHandle()) },
                        onCancel = { navController.popBackStack() },
                        onSelect = { selection ->
                            navController.previousBackStackEntry?.savedStateHandle?.let {
                                PlayFieldSelectResult.deliver(it, selection)
                            }
                            navController.popBackStack()
                        },
                    )
                }
                composable(Routes.SETTINGS) {
                    SettingsScreen(
                        viewModel = viewModel { SettingsViewModel(session, profileSettings, appLanguage) },
                        consentStore = locationConsentStore,
                        onBack = { navController.popBackStack() },
                    )
                }
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

/**
 * The playfield's details view model, scoped to its nested graph [graphEntry]: the details and the
 * area screen get the same instance, and it reads the playfield id and editability from the
 * graph's arguments.
 */
@Composable
private fun playFieldDetailsViewModel(
    graphEntry: NavBackStackEntry,
    repository: PlayFieldRepository,
): PlayFieldDetailsViewModel = viewModel(viewModelStoreOwner = graphEntry) {
    val handle = createSavedStateHandle()
    PlayFieldDetailsViewModel(
        playFieldId = handle.get<String>(Routes.PLAYFIELD_ID).orEmpty(),
        editable = handle.get<Boolean>(Routes.PLAYFIELD_EDITABLE) ?: false,
        repository = repository,
        savedStateHandle = handle,
    )
}
