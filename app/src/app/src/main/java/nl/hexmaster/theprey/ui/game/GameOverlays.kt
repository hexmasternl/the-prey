package nl.hexmaster.theprey.ui.game

import android.os.SystemClock
import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.widthIn
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableLongStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.semantics.clearAndSetSemantics
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.liveRegion
import androidx.compose.ui.semantics.LiveRegionMode
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.tooling.preview.Preview
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.compose.ui.window.DialogProperties
import kotlinx.coroutines.delay
import kotlinx.coroutines.isActive
import nl.hexmaster.theprey.R
import nl.hexmaster.theprey.ui.theme.ThePreyTheme

/**
 * The hunter's start-delay countdown over the map: a centred card on a light scrim with `mm:ss`
 * and the role's instruction. Ticks every second on the device's monotonic clock and draws
 * nothing once the delay is over. Takes no touches, so the map can still be panned.
 */
@Composable
fun StartDelayOverlay(
    seed: DelaySeed,
    role: GameRole,
    modifier: Modifier = Modifier,
    now: () -> Long = SystemClock::elapsedRealtime,
) {
    var remaining by remember(seed) { mutableLongStateOf(StartDelay.remaining(seed, now())) }
    LaunchedEffect(seed) {
        while (isActive && remaining > 0) {
            delay(TICK_MS)
            remaining = StartDelay.remaining(seed, now())
        }
    }
    if (remaining <= 0) return
    val time = StartDelay.format(remaining)
    val hunter = role == GameRole.Hunter
    CenteredCard(modifier) {
        val description = stringResource(R.string.game_delay_a11y, time)
        Text(
            text = time,
            style = MaterialTheme.typography.displayLarge.copy(fontSize = 64.sp),
            color = MaterialTheme.colorScheme.onSurface,
            modifier = Modifier.clearAndSetSemantics {
                contentDescription = description
                liveRegion = LiveRegionMode.Polite
            },
        )
        Spacer(Modifier.height(12.dp))
        Text(
            text = stringResource(if (hunter) R.string.game_delay_hunter else R.string.game_delay_prey),
            style = MaterialTheme.typography.bodyLarge,
            color = if (hunter) ThePreyTheme.colors.hunter else ThePreyTheme.colors.prey,
            textAlign = TextAlign.Center,
        )
    }
}

/** "Game is starting…" while the game is `Started` and its clock does not run yet. */
@Composable
fun StartingOverlay(modifier: Modifier = Modifier) {
    CenteredCard(modifier) {
        CircularProgressIndicator(color = MaterialTheme.colorScheme.primary)
        Spacer(Modifier.height(16.dp))
        Text(
            text = stringResource(R.string.game_starting),
            style = MaterialTheme.typography.titleLarge,
            color = MaterialTheme.colorScheme.onSurface,
            textAlign = TextAlign.Center,
        )
    }
}

@Composable
private fun CenteredCard(modifier: Modifier, content: @Composable () -> Unit) {
    Box(
        modifier.fillMaxSize().background(ThePreyTheme.colors.void.copy(alpha = SCRIM_ALPHA)),
        contentAlignment = Alignment.Center,
    ) {
        Column(
            Modifier
                .padding(24.dp)
                .widthIn(max = 360.dp)
                .background(MaterialTheme.colorScheme.surface.copy(alpha = 0.92f), MaterialTheme.shapes.large)
                .border(BorderStroke(1.dp, MaterialTheme.colorScheme.outlineVariant), MaterialTheme.shapes.large)
                .padding(24.dp),
            horizontalAlignment = Alignment.CenterHorizontally,
        ) {
            content()
        }
    }
}

/**
 * The traffic-safety warning shown on every entry. Only its button closes it: outside taps and
 * Back are ignored.
 */
@Composable
fun SafetyWarningDialog(onAcknowledge: () -> Unit) {
    AlertDialog(
        onDismissRequest = {},
        properties = DialogProperties(dismissOnBackPress = false, dismissOnClickOutside = false),
        containerColor = MaterialTheme.colorScheme.surface,
        titleContentColor = MaterialTheme.colorScheme.onSurface,
        textContentColor = MaterialTheme.colorScheme.onSurfaceVariant,
        title = { Text(stringResource(R.string.game_warning_title), style = MaterialTheme.typography.headlineSmall) },
        text = { Text(stringResource(R.string.game_warning_body), style = MaterialTheme.typography.bodyMedium) },
        confirmButton = {
            TextButton(onClick = onAcknowledge) {
                Text(
                    text = stringResource(R.string.game_warning_ack).uppercase(),
                    style = MaterialTheme.typography.labelLarge,
                    color = MaterialTheme.colorScheme.primary,
                )
            }
        },
    )
}

private const val TICK_MS = 1_000L
private const val SCRIM_ALPHA = 0.35f

// ---- Previews ----

@Composable
private fun OverlayPreview(content: @Composable () -> Unit) {
    ThePreyTheme { Box(Modifier.fillMaxSize().background(Color(0xFF2A3A2A))) { content() } }
}

@Preview(name = "Delay (prey)")
@Composable
private fun PreyDelayPreview() = OverlayPreview { StartDelayOverlay(DelaySeed(200_000, 0), GameRole.Prey, now = { 0 }) }

@Preview(name = "Delay (hunter, Dutch)", locale = "nl")
@Composable
private fun HunterDelayPreview() = OverlayPreview { StartDelayOverlay(DelaySeed(200_000, 0), GameRole.Hunter, now = { 0 }) }

@Preview(name = "Starting")
@Composable
private fun StartingPreview() = OverlayPreview { StartingOverlay() }

@Preview(name = "Safety warning (Dutch)", locale = "nl")
@Composable
private fun WarningPreview() = OverlayPreview { SafetyWarningDialog(onAcknowledge = {}) }
