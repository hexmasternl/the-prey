package nl.hexmaster.theprey.ui.navigation

import androidx.compose.foundation.layout.Box
import android.os.SystemClock
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
import androidx.navigation.NavController
import androidx.navigation.NavGraphBuilder
import androidx.navigation.NavHostController
import androidx.navigation.NavType
import androidx.navigation.compose.NavHost
import androidx.navigation.compose.composable
import androidx.navigation.compose.currentBackStackEntryAsState
import androidx.navigation.compose.navigation
import androidx.navigation.compose.rememberNavController
import androidx.navigation.navArgument
import nl.hexmaster.theprey.BuildConfig
import nl.hexmaster.theprey.auth.ProfileSettings
import nl.hexmaster.theprey.auth.SessionController
import nl.hexmaster.theprey.auth.SessionState
import nl.hexmaster.theprey.data.display.DisplayPreferencesStore
import nl.hexmaster.theprey.data.game.GameRepository
import nl.hexmaster.theprey.data.game.GameSessions
import nl.hexmaster.theprey.data.hints.HintsStore
import nl.hexmaster.theprey.data.language.AppLanguage
import nl.hexmaster.theprey.data.location.LocationConsentStore
import nl.hexmaster.theprey.data.location.LocationSource
import nl.hexmaster.theprey.data.playfield.PlayFieldRepository
import nl.hexmaster.theprey.data.sensors.HeadingSource
import nl.hexmaster.theprey.data.user.ProfileStore
import nl.hexmaster.theprey.deeplink.PendingInviteStore
import nl.hexmaster.theprey.tracking.TrackingStarter
import nl.hexmaster.theprey.ui.consent.LocationConsentHost
import nl.hexmaster.theprey.ui.game.GameExit
import nl.hexmaster.theprey.ui.game.GameScreen
import nl.hexmaster.theprey.ui.game.GameViewModel
import nl.hexmaster.theprey.ui.game.hud.hunter.HunterHud
import nl.hexmaster.theprey.ui.game.hud.hunter.TagSheetViewModel
import nl.hexmaster.theprey.ui.game.hud.prey.PreyHud
import nl.hexmaster.theprey.ui.games.create.CreateGameScreen
import nl.hexmaster.theprey.ui.games.create.CreateGameViewModel
import nl.hexmaster.theprey.ui.games.join.JoinExit
import nl.hexmaster.theprey.ui.games.join.JoinGameScreen
import nl.hexmaster.theprey.ui.games.join.JoinGameViewModel
import nl.hexmaster.theprey.ui.games.lobby.LobbyExit
import nl.hexmaster.theprey.ui.games.lobby.LobbyScreen
import nl.hexmaster.theprey.ui.games.lobby.LobbyViewModel
import nl.hexmaster.theprey.ui.landing.LandingScreen
import nl.hexmaster.theprey.ui.landing.LandingViewModel
import nl.hexmaster.theprey.ui.mainmenu.MainMenuMessage
import nl.hexmaster.theprey.ui.mainmenu.MainMenuScreen
import nl.hexmaster.theprey.ui.mainmenu.MainMenuViewModel
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
    /** Creates a game; opened by Play now. Starts with the playfield selection screen. */
    const val GAME_CREATE = "game_create"

    /** The lobby of a game that has not started yet. */
    const val LOBBY = "lobby/{gameId}"

    fun lobby(gameId: String) = "lobby/$gameId"

    /** A running game the player takes part in; opened by Resume game. */
    const val GAME = "game/{gameId}"
    const val GAME_ID = "gameId"

    fun game(gameId: String) = "game/$gameId"

    /** Joins the game of a pending invitation; opened by itself while one is pending. */
    const val JOIN = "join"
    const val PLAYFIELDS = "playfields"

    /**
     * Creates a new playfield; opened by the create button on the Private tab. A nested graph like
     * [PLAYFIELD], with the details screen in create mode (start) and the area map.
     */
    const val PLAYFIELD_EDITOR = "playfield_editor"
    const val PLAYFIELD_NEW_DETAILS = "playfield_new_details"
    const val PLAYFIELD_NEW_AREA = "playfield_new_area"

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
 * [trackingRequest] carries a tap on the resume notification. An invitation in [pendingInviteStore]
 * opens the join step as soon as the player is signed in.
 */
@Composable
fun ThePreyNavHost(
    session: SessionController,
    locationSource: LocationSource,
    locationConsentStore: LocationConsentStore,
    gameRepository: GameRepository,
    playFieldRepository: PlayFieldRepository,
    profileStore: ProfileStore,
    gameSessions: GameSessions,
    headingSource: HeadingSource,
    profileSettings: ProfileSettings,
    appLanguage: AppLanguage,
    pendingInviteStore: PendingInviteStore,
    hintsStore: HintsStore,
    displayPreferencesStore: DisplayPreferencesStore,
    modifier: Modifier = Modifier,
    trackingStarter: TrackingStarter? = null,
    trackingRequest: TrackingRequest? = null,
    navController: NavHostController = rememberNavController(),
) {
    val state by session.state.collectAsStateWithLifecycle()
    val hasPendingInvite by pendingInviteStore.pending.collectAsStateWithLifecycle(initialValue = false)
    // Nothing to show yet: the system splash stays up until the session is known.
    val target = routeFor(state) ?: return

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
                composable(Routes.MAIN_MENU) { entry ->
                    Box {
                        MainMenuScreen(
                        viewModel = viewModel { MainMenuViewModel(session, locationSource, gameRepository, BuildConfig.VERSION_NAME) },
                        onPlayNow = { navController.navigate(Routes.GAME_CREATE) },
                        onResumeGame = { gameId -> navController.navigate(Routes.game(gameId)) },
                        onPlayfields = { navController.navigate(Routes.PLAYFIELDS) },
                        onSettings = { navController.navigate(Routes.SETTINGS) },
                        )
                        MainMenuMessage.Host(entry.savedStateHandle)
                    }
                }
                // Scoped to this entry: every Play now starts with the defaults.
                composable(Routes.GAME_CREATE) { entry ->
                    CreateGameScreen(
                        viewModel = viewModel { CreateGameViewModel(gameRepository, profileStore, createSavedStateHandle()) },
                        resultHandle = entry.savedStateHandle,
                        onBack = { if (navController.currentBackStackEntry == entry) navController.popBackStack() },
                        onChoosePlayField = {
                            if (navController.currentBackStackEntry == entry) navController.navigate(Routes.PLAYFIELD_SELECT)
                        },
                        // The create screen leaves the back stack: Back from the lobby returns to the menu.
                        onCreated = { gameId ->
                            navController.navigate(Routes.lobby(gameId)) {
                                popUpTo(Routes.GAME_CREATE) { inclusive = true }
                            }
                        },
                    )
                }
                composable(Routes.LOBBY, arguments = listOf(navArgument(Routes.GAME_ID) { type = NavType.StringType })) { entry ->
                    val gameId = entry.arguments?.getString(Routes.GAME_ID).orEmpty()
                    LobbyScreen(
                        viewModel = viewModel { LobbyViewModel(gameId, gameRepository, gameSessions, profileStore) },
                        // The lobby leaves the back stack: Back from the game returns to the menu.
                        onStarted = { id ->
                            if (navController.currentBackStackEntry == entry) {
                                navController.navigate(Routes.game(id)) { popUpTo(Routes.LOBBY) { inclusive = true } }
                            }
                        },
                        onExit = { reason ->
                            if (navController.currentBackStackEntry == entry) {
                                val message = when (reason) {
                                    LobbyExit.Left -> null
                                    LobbyExit.Cancelled -> MainMenuMessage.GameCancelled
                                    LobbyExit.Removed -> MainMenuMessage.RemovedFromGame
                                    LobbyExit.Ended -> MainMenuMessage.GameEnded
                                }
                                navController.returnToMainMenu(message)
                            }
                        },
                    )
                }
                // A new entry per visit: every entry downloads a fresh snapshot and shows the safety warning.
                composable(Routes.GAME, arguments = listOf(navArgument(Routes.GAME_ID) { type = NavType.StringType })) { entry ->
                    val gameId = entry.arguments?.getString(Routes.GAME_ID).orEmpty()
                    GameScreen(
                        viewModel = viewModel {
                            GameViewModel(gameId, gameSessions, profileStore, locationSource, headingSource, SystemClock::elapsedRealtime)
                        },
                        preyHud = { ready -> PreyHud(ready) },
                        hunterHud = { ready ->
                            HunterHud(
                                ready = ready,
                                tagSheetViewModel = {
                                    viewModel(key = "tag_sheet") { TagSheetViewModel(gameId, gameRepository, gameSessions) }
                                },
                            )
                        },
                        hints = hintsStore,
                        displayPreferences = displayPreferencesStore,
                        // Back leaves the screen, not the game: Resume game on the menu reopens it.
                        onExit = { reason ->
                            if (navController.currentBackStackEntry == entry) {
                                navController.returnToMainMenu(
                                    when (reason) {
                                        null -> null
                                        GameExit.Ended -> MainMenuMessage.GameEnded
                                        GameExit.Cancelled -> MainMenuMessage.GameCancelled
                                        GameExit.Unavailable -> MainMenuMessage.GameUnavailable
                                        // The result dialog already told the player how the game ended.
                                        GameExit.Dismissed -> null
                                    },
                                )
                            }
                        },
                    )
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
                    playFieldDetailsDestination(
                        route = Routes.PLAYFIELD_DETAILS,
                        graphRoute = Routes.PLAYFIELD,
                        areaRoute = Routes.PLAYFIELD_AREA,
                        navController = navController,
                        repository = playFieldRepository,
                    )
                    playFieldAreaDestination(
                        route = Routes.PLAYFIELD_AREA,
                        graphRoute = Routes.PLAYFIELD,
                        navController = navController,
                        repository = playFieldRepository,
                        locationSource = locationSource,
                    )
                }
                // The same two screens in create mode: no playfield id, so nothing is downloaded and Save creates.
                navigation(route = Routes.PLAYFIELD_EDITOR, startDestination = Routes.PLAYFIELD_NEW_DETAILS) {
                    playFieldDetailsDestination(
                        route = Routes.PLAYFIELD_NEW_DETAILS,
                        graphRoute = Routes.PLAYFIELD_EDITOR,
                        areaRoute = Routes.PLAYFIELD_NEW_AREA,
                        navController = navController,
                        repository = playFieldRepository,
                    )
                    playFieldAreaDestination(
                        route = Routes.PLAYFIELD_NEW_AREA,
                        graphRoute = Routes.PLAYFIELD_EDITOR,
                        navController = navController,
                        repository = playFieldRepository,
                        locationSource = locationSource,
                        // Only a new playfield's area gets the first-time tour.
                        hints = hintsStore,
                    )
                }
                // Opened by a pending invitation; every exit leaves this entry.
                composable(Routes.JOIN) { entry ->
                    JoinGameScreen(
                        viewModel = viewModel { JoinGameViewModel(pendingInviteStore, gameRepository, profileStore) },
                        onExit = { exit ->
                            if (navController.currentBackStackEntry == entry) {
                                when (exit) {
                                    is JoinExit.Lobby -> navController.navigate(Routes.lobby(exit.gameId)) {
                                        popUpTo(Routes.JOIN) { inclusive = true }
                                    }
                                    is JoinExit.Game -> navController.navigate(Routes.game(exit.gameId)) {
                                        popUpTo(Routes.JOIN) { inclusive = true }
                                    }
                                    is JoinExit.Menu -> navController.returnToMainMenu(exit.message)
                                }
                            }
                        },
                    )
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
                        viewModel = viewModel {
                            SettingsViewModel(session, profileSettings, appLanguage, hintsStore, displayPreferencesStore)
                        },
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

    // A pending invitation opens the join step once the player is in the signed-in area, on top of
    // whatever is showing; signed out, it waits on the landing page until the sign-in completes.
    val signedIn = state is SessionState.SignedIn
    val currentDestination = currentEntry?.destination
    LaunchedEffect(signedIn, hasPendingInvite, currentDestination) {
        val inSignedInArea = currentDestination?.hierarchy?.any { it.route == Routes.SIGNED_IN } == true
        if (signedIn && hasPendingInvite && inSignedInArea && currentDestination.route != Routes.JOIN) {
            navController.navigate(Routes.JOIN) { launchSingleTop = true }
        }
    }
}

/** Pops back to the main menu, posting [message] to show there (null: none). */
private fun NavController.returnToMainMenu(message: MainMenuMessage?) {
    if (message != null) {
        runCatching { getBackStackEntry(Routes.MAIN_MENU) }.getOrNull()?.savedStateHandle?.let { MainMenuMessage.post(it, message) }
    }
    if (!popBackStack(Routes.MAIN_MENU, inclusive = false)) navigate(Routes.MAIN_MENU)
}

/**
 * The details screen of the playfield graph [graphRoute] (edit or create). Save, Cancel and Back
 * leave the whole graph, which discards the draft.
 */
private fun NavGraphBuilder.playFieldDetailsDestination(
    route: String,
    graphRoute: String,
    areaRoute: String,
    navController: NavController,
    repository: PlayFieldRepository,
) {
    composable(route) { entry ->
        val graphEntry = remember(entry) { navController.getBackStackEntry(graphRoute) }
        PlayFieldDetailsScreen(
            viewModel = playFieldDetailsViewModel(graphEntry, repository),
            onClose = {
                if (navController.currentBackStackEntry == entry) navController.popBackStack(graphRoute, inclusive = true)
            },
            onOpenArea = {
                if (navController.currentBackStackEntry == entry) navController.navigate(areaRoute)
            },
        )
    }
}

/** The area map of the playfield graph [graphRoute]; its Save hands the points to the details draft. */
private fun NavGraphBuilder.playFieldAreaDestination(
    route: String,
    graphRoute: String,
    navController: NavController,
    repository: PlayFieldRepository,
    locationSource: LocationSource,
    hints: HintsStore? = null,
) {
    composable(route) { entry ->
        val graphEntry = remember(entry) { navController.getBackStackEntry(graphRoute) }
        val details = playFieldDetailsViewModel(graphEntry, repository)
        PlayFieldAreaScreen(
            viewModel = viewModel {
                val draft = details.uiState.value
                PlayFieldAreaViewModel(draft.points, draft.editable, createSavedStateHandle(), details::applyArea)
            },
            locationSource = locationSource,
            onClose = { if (navController.currentBackStackEntry == entry) navController.popBackStack() },
            hints = hints,
        )
    }
}

/**
 * The playfield's details view model, scoped to its nested graph [graphEntry]: the details and the
 * area screen get the same instance, and it reads the playfield id and editability from the
 * graph's arguments. The editor graph has no id: create mode, always editable.
 */
@Composable
private fun playFieldDetailsViewModel(
    graphEntry: NavBackStackEntry,
    repository: PlayFieldRepository,
): PlayFieldDetailsViewModel = viewModel(viewModelStoreOwner = graphEntry) {
    val handle = createSavedStateHandle()
    val playFieldId = handle.get<String>(Routes.PLAYFIELD_ID)
    PlayFieldDetailsViewModel(
        playFieldId = playFieldId,
        editable = playFieldId == null || (handle.get<Boolean>(Routes.PLAYFIELD_EDITABLE) ?: false),
        repository = repository,
        savedStateHandle = handle,
    )
}
