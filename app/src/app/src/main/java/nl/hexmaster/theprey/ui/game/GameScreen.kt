package nl.hexmaster.theprey.ui.game

import android.os.SystemClock
import androidx.activity.compose.BackHandler
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.material3.MaterialTheme
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableLongStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberUpdatedState
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.res.pluralStringResource
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.tooling.preview.Preview
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import kotlinx.coroutines.delay
import kotlinx.coroutines.isActive
import nl.hexmaster.theprey.R
import nl.hexmaster.theprey.data.location.GeoPosition
import nl.hexmaster.theprey.ui.components.rememberReducedMotion
import nl.hexmaster.theprey.ui.game.hud.common.HudRole
import nl.hexmaster.theprey.ui.game.hud.common.hudRoleFor
import nl.hexmaster.theprey.ui.game.map.GameMap
import nl.hexmaster.theprey.ui.game.map.MapMarker
import nl.hexmaster.theprey.ui.game.map.MarkerStyle
import nl.hexmaster.theprey.ui.game.result.GameResult
import nl.hexmaster.theprey.ui.game.result.GameResultDialog
import nl.hexmaster.theprey.ui.game.result.GameResultKind
import nl.hexmaster.theprey.ui.playfields.MessageState
import nl.hexmaster.theprey.ui.theme.ThePreyTheme
import java.time.Instant

/** A role HUD drawn in the game screen's HUD slot. */
typealias GameHud = @Composable (state: ActiveGameUi.Ready) -> Unit

/**
 * The active game screen. Layers, bottom to top: the live map, the starting / start-delay
 * overlay, the HUD of the player's role ([preyHud] or [hunterHud]), and the traffic-safety warning,
 * which is shown on every entry (not again after a rotation). Back returns to the main menu
 * without leaving the game ([onExit] with null); the game ending does so with its reason. A game
 * played out with a decided result shows the result dialog instead, whose button and Back go to the
 * menu with [GameExit.Dismissed].
 */
@Composable
fun GameScreen(
    viewModel: GameViewModel,
    onExit: (GameExit?) -> Unit,
    modifier: Modifier = Modifier,
    preyHud: GameHud = {},
    hunterHud: GameHud = {},
) {
    val ui by viewModel.uiState.collectAsStateWithLifecycle()
    var warningAcknowledged by rememberSaveable { mutableStateOf(false) }
    val currentOnExit by rememberUpdatedState(onExit)

    LaunchedEffect(ui) {
        (ui as? ActiveGameUi.Exit)?.let { currentOnExit(it.reason) }
    }
    // While the warning is open its dialog takes Back itself, so Back does nothing then.
    // Over a result, Back dismisses it like its button.
    BackHandler { currentOnExit(if (ui is ActiveGameUi.Result) GameExit.Dismissed else null) }

    GameContent(
        ui = ui,
        onRetry = viewModel::retry,
        preyHud = preyHud,
        hunterHud = hunterHud,
        modifier = modifier,
    )
    when {
        ui is ActiveGameUi.Result -> GameResultDialog(
            result = (ui as ActiveGameUi.Result).result,
            onDismiss = { currentOnExit(GameExit.Dismissed) },
        )
        // The game is over: nothing left to warn about.
        !warningAcknowledged -> SafetyWarningDialog(onAcknowledge = { warningAcknowledged = true })
    }
}

@Composable
private fun GameContent(
    ui: ActiveGameUi,
    onRetry: () -> Unit,
    preyHud: GameHud,
    hunterHud: GameHud,
    modifier: Modifier = Modifier,
    reducedMotion: Boolean = rememberReducedMotion(),
) {
    Box(modifier.fillMaxSize().background(MaterialTheme.colorScheme.background)) {
        when (ui) {
            ActiveGameUi.Loading, is ActiveGameUi.Exit -> MessageState(text = null, loading = true)
            // The session is gone, so there is no map: the background under the result dialog.
            is ActiveGameUi.Result -> Unit
            is ActiveGameUi.Error -> MessageState(
                text = stringResource(R.string.game_load_error),
                actionLabel = if (ui.retryable) stringResource(R.string.playfields_retry) else null,
                onAction = onRetry,
            )
            is ActiveGameUi.Ready -> {
                val serverNow = rememberServerNow(ui)
                val markers = remember(ui.game, serverNow) { markersFor(ui.game, serverNow) }
                GameMap(
                    area = ui.game.area,
                    areaRole = ui.game.role,
                    markers = markers,
                    own = ui.own,
                    heading = ui.heading,
                    reducedMotion = reducedMotion,
                    contentDescription = mapDescription(ui.game.role, markers.size),
                    modifier = Modifier.fillMaxSize(),
                )
                when {
                    ui.game.phase == GamePhase.Starting -> StartingOverlay()
                    ui.delay != null -> StartDelayOverlay(ui.delay, ui.game.role)
                }
                when (hudRoleFor(ui.game)) {
                    HudRole.Prey -> preyHud(ui)
                    HudRole.Hunter -> hunterHud(ui)
                    HudRole.None -> Unit
                }
            }
        }
    }
}

/** The corrected server time, ticking once a second so penalties expire on the map without a message. */
@Composable
private fun rememberServerNow(ui: ActiveGameUi.Ready): Instant {
    var elapsed by remember { mutableLongStateOf(SystemClock.elapsedRealtime()) }
    LaunchedEffect(Unit) {
        while (isActive) {
            delay(1_000)
            elapsed = SystemClock.elapsedRealtime()
        }
    }
    return ui.clock?.now(elapsed) ?: Instant.now()
}

/** The other participants the viewer may see, each with its style. */
internal fun markersFor(game: ActiveGameState, serverNow: Instant): List<MapMarker> =
    game.participants.mapNotNull { participant ->
        val position: GeoPosition = participant.location ?: return@mapNotNull null
        MarkerStyle.of(game.role, participant, serverNow)?.let { MapMarker(participant.userId, position, it) }
    }

@Composable
private fun mapDescription(role: GameRole, shown: Int): String {
    val roleName = stringResource(
        when (role) {
            GameRole.Hunter -> R.string.game_role_hunter
            GameRole.Prey -> R.string.game_role_prey
            GameRole.None -> R.string.game_role_spectator
        },
    )
    return pluralStringResource(R.plurals.game_map_description, shown, roleName, shown)
}

// ---- Previews (the map needs a device; these show the layers above it) ----

@Preview(name = "Loading with warning")
@Composable
private fun LoadingPreview() {
    ThePreyTheme {
        GameContent(ui = ActiveGameUi.Loading, onRetry = {}, preyHud = {}, hunterHud = {}, reducedMotion = true)
        SafetyWarningDialog(onAcknowledge = {})
    }
}

@Preview(name = "Result")
@Composable
private fun ResultPreview() {
    val ui = ActiveGameUi.Result(GameResult(GameResultKind.PreyWon, escaped = 2, preys = 5))
    ThePreyTheme {
        GameContent(ui = ui, onRetry = {}, preyHud = {}, hunterHud = {}, reducedMotion = true)
        GameResultDialog(result = ui.result, onDismiss = {}, reducedMotion = true)
    }
}

@Preview(name = "Error (Dutch)", locale = "nl")
@Composable
private fun ErrorPreview() {
    ThePreyTheme { GameContent(ui = ActiveGameUi.Error(retryable = true), onRetry = {}, preyHud = {}, hunterHud = {}) }
}
