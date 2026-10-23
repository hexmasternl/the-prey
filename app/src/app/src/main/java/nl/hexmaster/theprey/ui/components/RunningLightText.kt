package nl.hexmaster.theprey.ui.components

import androidx.compose.animation.core.Animatable
import androidx.compose.animation.core.LinearEasing
import androidx.compose.animation.core.tween
import androidx.compose.material3.LocalTextStyle
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.remember
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.lerp
import androidx.compose.ui.text.SpanStyle
import androidx.compose.ui.text.TextStyle
import androidx.compose.ui.text.buildAnnotatedString
import androidx.compose.ui.text.withStyle
import androidx.compose.ui.tooling.preview.Preview
import kotlinx.coroutines.delay
import nl.hexmaster.theprey.ui.theme.ThePreyTheme
import kotlin.math.abs

private const val SWEEP_MS = 1_200
private const val PERIOD_MS = 5_000L
private const val FIRST_SWEEP_DELAY_MS = 800L
private const val LIGHT_WIDTH = 3f

/**
 * How lit character [index] is (0..1) when the running light's centre is at [head] (in characters):
 * 1 at the head, falling off linearly to 0 at [width] characters away.
 */
fun runningLightIntensity(index: Int, head: Float, width: Float): Float =
    (1f - abs(index - head) / width).coerceIn(0f, 1f)

/**
 * [text] in [color], with a [lightColor] highlight that sweeps across it letter by letter once
 * every 5 seconds. Shows plain text when [animate] is false.
 */
@Composable
fun RunningLightText(
    text: String,
    modifier: Modifier = Modifier,
    style: TextStyle = LocalTextStyle.current,
    color: Color = MaterialTheme.colorScheme.onBackground,
    lightColor: Color = MaterialTheme.colorScheme.primary,
    animate: Boolean = !rememberReducedMotion(),
) {
    // Parked before the first character: nothing lit.
    val head = remember { Animatable(-LIGHT_WIDTH) }
    if (animate) {
        LaunchedEffect(text) {
            delay(FIRST_SWEEP_DELAY_MS)
            while (true) {
                head.snapTo(-LIGHT_WIDTH)
                head.animateTo(text.length + LIGHT_WIDTH, tween(SWEEP_MS, easing = LinearEasing))
                delay(PERIOD_MS - SWEEP_MS)
            }
        }
    }

    val position = if (animate) head.value else -LIGHT_WIDTH
    val annotated = buildAnnotatedString {
        text.forEachIndexed { index, char ->
            val intensity = runningLightIntensity(index, position, LIGHT_WIDTH)
            withStyle(SpanStyle(color = lerp(color, lightColor, intensity))) { append(char) }
        }
    }
    Text(text = annotated, modifier = modifier, style = style)
}

@Preview
@Composable
private fun RunningLightTextPreview() {
    ThePreyTheme { RunningLightText(text = "NIGHT-HAWK_7", animate = false) }
}
