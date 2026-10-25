package nl.hexmaster.theprey.ui.game.result

import androidx.annotation.StringRes
import androidx.compose.animation.core.Animatable
import androidx.compose.animation.core.CubicBezierEasing
import androidx.compose.animation.core.tween
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.graphicsLayer
import androidx.compose.ui.res.pluralStringResource
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.tooling.preview.Preview
import androidx.compose.ui.unit.dp
import androidx.compose.ui.window.DialogProperties
import nl.hexmaster.theprey.R
import nl.hexmaster.theprey.ui.components.PreyMark
import nl.hexmaster.theprey.ui.components.PrimaryButton
import nl.hexmaster.theprey.ui.components.rememberReducedMotion
import nl.hexmaster.theprey.ui.theme.ThePreyTheme

/** What one version of the dialog shows. */
private data class ResultVersion(
    @StringRes val title: Int,
    @StringRes val message: Int,
    @StringRes val role: Int,
)

private val versions = mapOf(
    GameResultKind.HunterWon to ResultVersion(
        R.string.game_result_hunter_won_title,
        R.string.game_result_hunter_won_message,
        R.string.game_role_hunter,
    ),
    GameResultKind.HunterLost to ResultVersion(
        R.string.game_result_hunter_lost_title,
        R.string.game_result_hunter_lost_message,
        R.string.game_role_hunter,
    ),
    GameResultKind.PreyWon to ResultVersion(
        R.string.game_result_prey_won_title,
        R.string.game_result_prey_won_message,
        R.string.game_role_prey,
    ),
    GameResultKind.PreyLost to ResultVersion(
        R.string.game_result_prey_lost_title,
        R.string.game_result_prey_lost_message,
        R.string.game_role_prey,
    ),
)

/**
 * The end-of-game result: one modal for all four [GameResultKind]s, picked from [result]. A win is
 * accented in signal green and plays a one-shot celebration (a pulse ring behind the mark and a
 * scale-in of the title); a loss is accented in hunter red and does not animate. Reduced motion
 * draws the end frame. The button and Back call [onDismiss]; an outside tap does nothing.
 */
@Composable
fun GameResultDialog(
    result: GameResult,
    onDismiss: () -> Unit,
    reducedMotion: Boolean = rememberReducedMotion(),
) {
    val version = versions.getValue(result.kind)
    val accent = if (result.isWin) ThePreyTheme.colors.signal else ThePreyTheme.colors.hunter
    // Once per dialog, not again after a rotation.
    var celebrated by rememberSaveable { mutableStateOf(false) }
    val animate = result.isWin && !reducedMotion && !celebrated
    val progress = remember { Animatable(if (animate) 0f else 1f) }
    LaunchedEffect(animate) {
        if (animate) {
            progress.animateTo(1f, tween(CELEBRATION_MS, easing = EaseOut))
            celebrated = true
        } else {
            progress.snapTo(1f)
        }
    }
    val p = progress.value

    AlertDialog(
        onDismissRequest = onDismiss,
        properties = DialogProperties(dismissOnBackPress = true, dismissOnClickOutside = false),
        containerColor = MaterialTheme.colorScheme.surface,
        titleContentColor = MaterialTheme.colorScheme.onSurface,
        textContentColor = MaterialTheme.colorScheme.onSurfaceVariant,
        icon = {
            Box(Modifier.size(MARK_BOX), contentAlignment = Alignment.Center) {
                if (result.isWin && p < 1f) {
                    // The pulse ring grows from the mark and fades out.
                    Box(
                        Modifier
                            .fillMaxSize()
                            .graphicsLayer {
                                val scale = 0.6f + p
                                scaleX = scale
                                scaleY = scale
                                alpha = 1f - p
                            }
                            .border(2.dp, accent, CircleShape),
                    )
                }
                Box(
                    Modifier
                        .size(MARK_BOX)
                        .background(accent.copy(alpha = TINT_ALPHA), CircleShape)
                        .border(2.dp, accent, CircleShape),
                    contentAlignment = Alignment.Center,
                ) {
                    PreyMark(markSize = MARK_SIZE, contentDescription = stringResource(R.string.game_result_mark_description))
                }
            }
        },
        title = {
            Column(horizontalAlignment = Alignment.CenterHorizontally, modifier = Modifier.fillMaxWidth()) {
                Text(
                    text = stringResource(R.string.game_result_role, stringResource(version.role)).uppercase(),
                    style = MaterialTheme.typography.labelMedium,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )
                Text(
                    text = stringResource(version.title),
                    style = MaterialTheme.typography.headlineMedium,
                    color = accent,
                    textAlign = TextAlign.Center,
                    modifier = Modifier.graphicsLayer {
                        val scale = if (result.isWin) TITLE_START_SCALE + (1f - TITLE_START_SCALE) * p else 1f
                        scaleX = scale
                        scaleY = scale
                    },
                )
            }
        },
        text = {
            Column(
                horizontalAlignment = Alignment.CenterHorizontally,
                verticalArrangement = Arrangement.spacedBy(12.dp),
                modifier = Modifier.fillMaxWidth(),
            ) {
                Text(
                    text = stringResource(version.message),
                    style = MaterialTheme.typography.bodyMedium,
                    textAlign = TextAlign.Center,
                )
                Text(
                    text = pluralStringResource(R.plurals.game_result_escaped, result.preys, result.escaped, result.preys),
                    style = MaterialTheme.typography.bodyMedium,
                    color = MaterialTheme.colorScheme.onSurface,
                    textAlign = TextAlign.Center,
                )
            }
        },
        confirmButton = {
            PrimaryButton(
                text = stringResource(R.string.game_result_dismiss),
                onClick = onDismiss,
                modifier = Modifier.fillMaxWidth(),
            )
        },
    )
}

private const val CELEBRATION_MS = 900
private const val TITLE_START_SCALE = 0.8f
private const val TINT_ALPHA = 0.10f
private val MARK_BOX = 88.dp
private val MARK_SIZE = 56.dp
private val EaseOut = CubicBezierEasing(0.2f, 0.8f, 0.2f, 1f)

// ---- Previews (reduced motion: the end frame) ----

@Composable
private fun ResultPreview(kind: GameResultKind, escaped: Int = 2, preys: Int = 5) {
    ThePreyTheme {
        Box(Modifier.fillMaxSize().background(MaterialTheme.colorScheme.background)) {
            GameResultDialog(GameResult(kind, escaped, preys), onDismiss = {}, reducedMotion = true)
        }
    }
}

@Preview(name = "Hunter won")
@Composable
private fun HunterWonPreview() = ResultPreview(GameResultKind.HunterWon, escaped = 0)

@Preview(name = "Hunter lost")
@Composable
private fun HunterLostPreview() = ResultPreview(GameResultKind.HunterLost)

@Preview(name = "Prey won (Dutch)", locale = "nl")
@Composable
private fun PreyWonDutchPreview() = ResultPreview(GameResultKind.PreyWon)

@Preview(name = "Prey lost")
@Composable
private fun PreyLostPreview() = ResultPreview(GameResultKind.PreyLost, escaped = 0, preys = 1)
