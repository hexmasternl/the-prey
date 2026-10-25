package nl.hexmaster.theprey.ui.game.hud.prey

import androidx.compose.animation.AnimatedVisibility
import androidx.compose.animation.EnterTransition
import androidx.compose.animation.ExitTransition
import androidx.compose.animation.expandVertically
import androidx.compose.animation.shrinkVertically
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.IntrinsicSize
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.WindowInsets
import androidx.compose.foundation.layout.fillMaxHeight
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.safeDrawing
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.windowInsetsPadding
import androidx.compose.material3.Icon
import androidx.compose.material3.LinearProgressIndicator
import androidx.compose.material3.MaterialTheme
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.rotate
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.StrokeCap
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.semantics.Role
import androidx.compose.ui.tooling.preview.Preview
import androidx.compose.ui.unit.dp
import nl.hexmaster.theprey.R
import nl.hexmaster.theprey.ui.components.rememberReducedMotion
import nl.hexmaster.theprey.ui.game.ActiveGameUi
import nl.hexmaster.theprey.ui.game.hud.common.HudCountdown
import nl.hexmaster.theprey.ui.game.hud.common.HudPill
import nl.hexmaster.theprey.ui.game.hud.common.HudTile
import nl.hexmaster.theprey.ui.game.hud.common.broadcastProgress
import nl.hexmaster.theprey.ui.game.hud.common.formatDistance
import nl.hexmaster.theprey.ui.game.hud.common.formatGameTime
import nl.hexmaster.theprey.ui.game.hud.common.hudSurface
import nl.hexmaster.theprey.ui.game.hud.common.rememberElapsedTicker
import nl.hexmaster.theprey.ui.theme.ThePreyTheme
import java.time.Instant

/** The prey HUD in the game screen's slot, driven by a 1-second ticker over [PreyHudState]. */
@Composable
fun PreyHud(ready: ActiveGameUi.Ready, modifier: Modifier = Modifier) {
    val now = rememberElapsedTicker()
    val serverNow = ready.clock?.now(now) ?: Instant.now()
    val state = remember(ready, serverNow) { PreyHudState.from(ready, serverNow) }
    PreyHudContent(state = state, nowElapsed = now, modifier = modifier)
}

@Composable
internal fun PreyHudContent(
    state: PreyHudState,
    nowElapsed: Long,
    modifier: Modifier = Modifier,
    reducedMotion: Boolean = rememberReducedMotion(),
) {
    Box(modifier.fillMaxSize().windowInsetsPadding(WindowInsets.safeDrawing).padding(16.dp)) {
        RolePill(penaltyActive = state.penaltyActive, nowElapsed = nowElapsed, modifier = Modifier.align(Alignment.TopStart))
        ProximityPill(band = state.band, modifier = Modifier.align(Alignment.TopEnd))
        PreyHudPanel(state = state, nowElapsed = nowElapsed, reducedMotion = reducedMotion, modifier = Modifier.align(Alignment.BottomCenter))
    }
}

/**
 * "Prey" on green; while a penalty is active red, switching between "Penalty" and "Prey" every
 * second from the penalty's start. Clock-driven, so it keeps switching with animations off.
 */
@Composable
fun RolePill(penaltyActive: Boolean, nowElapsed: Long, modifier: Modifier = Modifier) {
    // The moment the penalty became active (re-captured each time it starts).
    val penaltySince = remember(penaltyActive) { nowElapsed }
    val label = if (penaltyActive) penaltyLabelAt(nowElapsed - penaltySince) else RoleLabel.Prey
    HudPill(
        text = stringResource(if (label == RoleLabel.Penalty) R.string.hud_role_penalty else R.string.hud_role_prey),
        color = if (penaltyActive) ThePreyTheme.colors.hunter else ThePreyTheme.colors.prey,
        onColor = if (penaltyActive) MaterialTheme.colorScheme.onSurface else ThePreyTheme.colors.void,
        description = stringResource(if (penaltyActive) R.string.hud_role_penalty_a11y else R.string.hud_role_prey_a11y),
        modifier = modifier,
    )
}

@Composable
internal fun ProximityBand.color(): Color = when (this) {
    ProximityBand.Safe -> ThePreyTheme.colors.prey
    ProximityBand.Close -> ThePreyTheme.colors.caution
    ProximityBand.Warning -> ThePreyTheme.colors.alert
    ProximityBand.Unknown -> ThePreyTheme.colors.textGhost
}

@Composable
private fun ProximityBand.label(): String = stringResource(
    when (this) {
        ProximityBand.Safe -> R.string.hud_band_safe
        ProximityBand.Close -> R.string.hud_band_close
        ProximityBand.Warning -> R.string.hud_band_warning
        ProximityBand.Unknown -> R.string.hud_band_unknown
    },
)

/** Safe (green), Close (yellow), Warning (orange) or a neutral grey Unknown. */
@Composable
fun ProximityPill(band: ProximityBand, modifier: Modifier = Modifier) {
    val label = band.label()
    HudPill(
        text = label,
        color = band.color(),
        onColor = if (band == ProximityBand.Unknown) MaterialTheme.colorScheme.onSurface else ThePreyTheme.colors.void,
        description = stringResource(R.string.hud_proximity_a11y, label),
        modifier = modifier,
    )
}

/**
 * The bottom panel. Expanded: game time and hunter distance side by side at equal size, the
 * next-broadcast tile and the arrow row. Collapsed: the next-broadcast tile and the arrow row.
 */
@Composable
fun PreyHudPanel(state: PreyHudState, nowElapsed: Long, reducedMotion: Boolean, modifier: Modifier = Modifier) {
    var expanded by rememberSaveable { mutableStateOf(true) }
    Column(modifier.fillMaxWidth(), verticalArrangement = Arrangement.spacedBy(8.dp)) {
        AnimatedVisibility(
            visible = expanded,
            enter = if (reducedMotion) EnterTransition.None else expandVertically(),
            exit = if (reducedMotion) ExitTransition.None else shrinkVertically(),
        ) {
            Row(Modifier.fillMaxWidth().height(IntrinsicSize.Max), horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                HudTile(
                    label = stringResource(R.string.hud_time_left),
                    value = formatGameTime(state.gameTime.secondsLeft(nowElapsed)),
                    valueColor = MaterialTheme.colorScheme.onSurface,
                    modifier = Modifier.weight(1f).fillMaxHeight(),
                )
                HudTile(
                    label = stringResource(R.string.hud_hunter_distance),
                    value = formatDistance(state.hunterDistanceMeters),
                    valueColor = state.band.color(),
                    modifier = Modifier.weight(1f).fillMaxHeight(),
                )
            }
        }
        val secondsLeft = state.nextBroadcast.secondsLeft(nowElapsed)
        HudTile(
            label = stringResource(R.string.hud_next_broadcast),
            value = stringResource(R.string.hud_seconds, secondsLeft),
            modifier = Modifier.fillMaxWidth(),
        ) {
            Spacer(Modifier.height(8.dp))
            LinearProgressIndicator(
                progress = { broadcastProgress(secondsLeft, state.broadcastIntervalSeconds) },
                modifier = Modifier.fillMaxWidth().height(6.dp),
                color = MaterialTheme.colorScheme.primary,
                trackColor = MaterialTheme.colorScheme.surfaceVariant,
                strokeCap = StrokeCap.Round,
                drawStopIndicator = {},
            )
        }
        val toggleLabel = stringResource(if (expanded) R.string.hud_panel_collapse else R.string.hud_panel_expand)
        Box(
            Modifier
                .fillMaxWidth()
                .heightIn(min = 48.dp)
                .background(hudSurface(), MaterialTheme.shapes.medium)
                .clickable(onClickLabel = toggleLabel, role = Role.Button) { expanded = !expanded },
            contentAlignment = Alignment.Center,
        ) {
            Icon(
                painter = painterResource(R.drawable.ic_chevron_right),
                contentDescription = toggleLabel,
                tint = MaterialTheme.colorScheme.onSurfaceVariant,
                modifier = Modifier.size(24.dp).rotate(if (expanded) 90f else -90f),
            )
        }
    }
}

// ---- Previews ----

private fun previewState(penalty: Boolean = false, meters: Int? = 180) = PreyHudState(
    penaltyActive = penalty,
    hunterDistanceMeters = meters,
    gameTime = HudCountdown(754, 0),
    nextBroadcast = HudCountdown(18, 0),
    broadcastIntervalSeconds = 30,
)

@Composable
private fun HudPreview(state: PreyHudState) {
    ThePreyTheme {
        Box(Modifier.fillMaxSize().background(Color(0xFF2A3A2A))) { PreyHudContent(state, nowElapsed = 0, reducedMotion = true) }
    }
}

@Preview(name = "Prey HUD", widthDp = 360, heightDp = 720)
@Composable
private fun PreyHudPreview() = HudPreview(previewState())

@Preview(name = "Prey HUD with penalty (Dutch)", locale = "nl", widthDp = 360, heightDp = 720)
@Composable
private fun PreyHudDutchPreview() = HudPreview(previewState(penalty = true, meters = 80))

@Preview(name = "Pills")
@Composable
private fun PillsPreview() {
    ThePreyTheme {
        Column(Modifier.background(Color(0xFF2A3A2A)).padding(12.dp), verticalArrangement = Arrangement.spacedBy(8.dp)) {
            RolePill(penaltyActive = false, nowElapsed = 0)
            RolePill(penaltyActive = true, nowElapsed = 0)
            ProximityBand.entries.forEach { ProximityPill(it) }
        }
    }
}
