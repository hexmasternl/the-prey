package nl.hexmaster.theprey.ui.game.hud.hunter

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.IntrinsicSize
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.WindowInsets
import androidx.compose.foundation.layout.fillMaxHeight
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.safeDrawing
import androidx.compose.foundation.layout.windowInsetsPadding
import androidx.compose.material3.ExtendedFloatingActionButton
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.SnackbarHost
import androidx.compose.material3.SnackbarHostState
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.LocalResources
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.semantics.stateDescription
import androidx.compose.ui.tooling.preview.Preview
import androidx.compose.ui.unit.dp
import kotlinx.coroutines.launch
import nl.hexmaster.theprey.R
import nl.hexmaster.theprey.ui.components.tour.tourTarget
import nl.hexmaster.theprey.ui.game.ActiveGameUi
import nl.hexmaster.theprey.ui.game.tour.GameTourTargets
import nl.hexmaster.theprey.ui.game.DelaySeed
import nl.hexmaster.theprey.ui.game.hud.common.HudCountdown
import nl.hexmaster.theprey.ui.game.hud.common.HudPill
import nl.hexmaster.theprey.ui.game.hud.common.HudTile
import nl.hexmaster.theprey.ui.game.hud.common.formatDistance
import nl.hexmaster.theprey.ui.game.hud.common.formatGameTime
import nl.hexmaster.theprey.ui.game.hud.common.rememberElapsedTicker
import nl.hexmaster.theprey.ui.theme.ThePreyTheme
import java.time.Instant

/**
 * The hunter HUD in the game screen's slot: role and game-clock pills, the Tag button above a
 * four-tile hunt summary, and the tag sheet. [tagSheetViewModel] creates the sheet's view model
 * (scoped by the caller) when the sheet is first opened.
 */
@Composable
fun HunterHud(
    ready: ActiveGameUi.Ready,
    tagSheetViewModel: @Composable () -> TagSheetViewModel,
    modifier: Modifier = Modifier,
) {
    val now = rememberElapsedTicker()
    val serverNow = ready.clock?.now(now) ?: Instant.now()
    val state = remember(ready, serverNow) { HunterHudState.from(ready, serverNow) }
    val snackbarHostState = remember { SnackbarHostState() }
    val scope = rememberCoroutineScope()
    val resources = LocalResources.current
    // Counts opens: the first creates the view model (which loads), later opens reload.
    var sheetOpens by rememberSaveable { mutableIntStateOf(0) }
    var sheetOpen by rememberSaveable { mutableStateOf(false) }

    HunterHudContent(
        state = state,
        nowElapsed = now,
        snackbarHostState = snackbarHostState,
        onTag = {
            sheetOpens++
            sheetOpen = true
        },
        modifier = modifier,
    )
    if (sheetOpen) {
        val viewModel = tagSheetViewModel()
        TagSheet(
            viewModel = viewModel,
            reload = sheetOpens > 1,
            onDismiss = { sheetOpen = false },
            onTagged = { callsign ->
                sheetOpen = false
                scope.launch { snackbarHostState.showSnackbar(resources.getString(R.string.tag_sheet_tagged, callsign)) }
            },
        )
    }
}

@Composable
internal fun HunterHudContent(
    state: HunterHudState,
    nowElapsed: Long,
    onTag: () -> Unit,
    modifier: Modifier = Modifier,
    snackbarHostState: SnackbarHostState = remember { SnackbarHostState() },
) {
    Box(modifier.fillMaxSize().windowInsetsPadding(WindowInsets.safeDrawing).padding(16.dp)) {
        HunterRolePill(Modifier.align(Alignment.TopStart).tourTarget(GameTourTargets.HUNTER_ROLE))
        GameClockPill(
            band = state.clockBand(nowElapsed),
            secondsLeft = state.gameTime.secondsLeft(nowElapsed),
            modifier = Modifier.align(Alignment.TopEnd).tourTarget(GameTourTargets.HUNTER_CLOCK),
        )
        Column(Modifier.align(Alignment.BottomCenter).fillMaxWidth(), verticalArrangement = Arrangement.spacedBy(12.dp)) {
            SnackbarHost(snackbarHostState)
            Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.End) {
                TagFab(
                    enabled = state.tagUnlock.isUnlocked(nowElapsed),
                    onClick = onTag,
                    modifier = Modifier.tourTarget(GameTourTargets.HUNTER_TAG),
                )
            }
            HunterSummaryGrid(state = state, nowElapsed = nowElapsed)
        }
    }
}

/** A red "Hunter" pill. */
@Composable
fun HunterRolePill(modifier: Modifier = Modifier) {
    HudPill(
        text = stringResource(R.string.hud_role_hunter),
        color = ThePreyTheme.colors.hunter,
        onColor = MaterialTheme.colorScheme.onSurface,
        description = stringResource(R.string.hud_role_hunter_a11y),
        modifier = modifier,
    )
}

@Composable
private fun ClockBand.color(): Color = when (this) {
    ClockBand.Normal -> ThePreyTheme.colors.prey
    ClockBand.Low -> ThePreyTheme.colors.alert
    ClockBand.Critical, ClockBand.Penalty -> ThePreyTheme.colors.hunter
}

/** "Live mm:ss" in the band colour, or red "Penalty mm:ss" while the hunter is penalised. */
@Composable
fun GameClockPill(band: ClockBand, secondsLeft: Int, modifier: Modifier = Modifier) {
    val time = formatGameTime(secondsLeft)
    val red = band == ClockBand.Critical || band == ClockBand.Penalty
    HudPill(
        text = stringResource(if (band == ClockBand.Penalty) R.string.hud_clock_penalty else R.string.hud_clock_live, time),
        color = band.color(),
        onColor = if (red) MaterialTheme.colorScheme.onSurface else ThePreyTheme.colors.void,
        modifier = modifier,
    )
}

/** Four equal tiles: next ping, preys in play, closest prey, penalised preys. */
@Composable
fun HunterSummaryGrid(state: HunterHudState, nowElapsed: Long, modifier: Modifier = Modifier) {
    Column(modifier.fillMaxWidth(), verticalArrangement = Arrangement.spacedBy(8.dp)) {
        Row(Modifier.fillMaxWidth().height(IntrinsicSize.Max), horizontalArrangement = Arrangement.spacedBy(8.dp)) {
            HudTile(
                label = stringResource(R.string.hud_next_ping),
                value = stringResource(R.string.hud_seconds, state.nextPing.secondsLeft(nowElapsed)),
                modifier = Modifier.weight(1f).fillMaxHeight().tourTarget(GameTourTargets.HUNTER_NEXT_PING),
            )
            HudTile(
                label = stringResource(R.string.hud_preys_in_play),
                value = stringResource(R.string.hud_preys_count, state.preysInPlay, state.preysTotal),
                modifier = Modifier.weight(1f).fillMaxHeight().tourTarget(GameTourTargets.HUNTER_PREYS_IN_PLAY),
            )
        }
        Row(Modifier.fillMaxWidth().height(IntrinsicSize.Max), horizontalArrangement = Arrangement.spacedBy(8.dp)) {
            HudTile(
                label = stringResource(R.string.hud_closest_prey),
                value = formatDistance(state.closestPreyMeters),
                valueColor = if (state.closestPreyMeters == null) ThePreyTheme.colors.textGhost else MaterialTheme.colorScheme.primary,
                modifier = Modifier.weight(1f).fillMaxHeight().tourTarget(GameTourTargets.HUNTER_CLOSEST_PREY),
            )
            HudTile(
                label = stringResource(R.string.hud_penalised_preys),
                value = state.penalisedPreys.toString(),
                // A zero value is inactive: Ghost (design system HUD rule).
                valueColor = if (state.penalisedPreys == 0) ThePreyTheme.colors.textGhost else ThePreyTheme.colors.hunter,
                modifier = Modifier.weight(1f).fillMaxHeight().tourTarget(GameTourTargets.HUNTER_PENALISED),
            )
        }
    }
}

/** The red Tag button with a target icon; disabled (and announced as locked) during the start delay. */
@Composable
fun TagFab(enabled: Boolean, onClick: () -> Unit, modifier: Modifier = Modifier) {
    val locked = stringResource(R.string.hud_tag_locked)
    val hunter = ThePreyTheme.colors.hunter
    ExtendedFloatingActionButton(
        onClick = { if (enabled) onClick() },
        icon = { Icon(painter = painterResource(R.drawable.ic_target), contentDescription = null) },
        text = { Text(stringResource(R.string.hud_tag).uppercase(), style = MaterialTheme.typography.labelLarge) },
        containerColor = if (enabled) hunter else MaterialTheme.colorScheme.surfaceVariant,
        contentColor = if (enabled) ThePreyTheme.colors.void else ThePreyTheme.colors.textGhost,
        modifier = modifier
            .heightIn(min = 56.dp)
            .semantics {
                if (!enabled) {
                    stateDescription = locked
                    contentDescription = locked
                }
            },
    )
}

// ---- Previews ----

private fun previewState(penalty: Boolean = false, seconds: Int = 754, locked: Boolean = false) = HunterHudState(
    hunterPenaltyActive = penalty,
    gameTime = HudCountdown(seconds, 0),
    nextPing = HudCountdown(18, 0),
    preysInPlay = 4,
    preysTotal = 6,
    closestPreyMeters = 85,
    penalisedPreys = 1,
    tagUnlock = TagUnlock(if (locked) DelaySeed(120_000, 0) else null),
)

@Composable
private fun HudPreview(state: HunterHudState) {
    ThePreyTheme {
        Box(Modifier.fillMaxSize().background(Color(0xFF3A2A2A))) { HunterHudContent(state, nowElapsed = 0, onTag = {}) }
    }
}

@Preview(name = "Hunter HUD", widthDp = 360, heightDp = 720)
@Composable
private fun HunterHudPreview() = HudPreview(previewState())

@Preview(name = "Hunter HUD locked (Dutch)", locale = "nl", widthDp = 360, heightDp = 720)
@Composable
private fun HunterHudDutchPreview() = HudPreview(previewState(locked = true))

@Preview(name = "Clock pills")
@Composable
private fun ClockPillsPreview() {
    ThePreyTheme {
        Column(Modifier.background(Color(0xFF3A2A2A)).padding(12.dp), verticalArrangement = Arrangement.spacedBy(8.dp)) {
            GameClockPill(ClockBand.Normal, 660)
            GameClockPill(ClockBand.Low, 600)
            GameClockPill(ClockBand.Critical, 300)
            GameClockPill(ClockBand.Penalty, 1_800)
            TagFab(enabled = true, onClick = {})
            TagFab(enabled = false, onClick = {})
        }
    }
}
