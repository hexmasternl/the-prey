package nl.hexmaster.theprey.ui.components

import androidx.compose.animation.core.CubicBezierEasing
import androidx.compose.animation.core.LinearEasing
import androidx.compose.animation.core.RepeatMode
import androidx.compose.animation.core.animateFloat
import androidx.compose.animation.core.infiniteRepeatable
import androidx.compose.animation.core.rememberInfiniteTransition
import androidx.compose.animation.core.tween
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.WindowInsets
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.safeDrawing
import androidx.compose.material3.MaterialTheme
import androidx.compose.runtime.Composable
import androidx.compose.runtime.State
import androidx.compose.runtime.mutableFloatStateOf
import androidx.compose.runtime.remember
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.drawWithCache
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.geometry.Size
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.Path
import androidx.compose.ui.graphics.StrokeCap
import androidx.compose.ui.graphics.drawscope.DrawScope
import androidx.compose.ui.graphics.drawscope.Stroke
import androidx.compose.ui.graphics.drawscope.rotate
import androidx.compose.ui.graphics.drawscope.translate
import androidx.compose.ui.platform.LocalDensity
import androidx.compose.ui.platform.LocalLayoutDirection
import androidx.compose.ui.semantics.clearAndSetSemantics
import androidx.compose.ui.tooling.preview.Preview
import androidx.compose.ui.unit.dp
import nl.hexmaster.theprey.ui.theme.ThePreyTheme
import kotlin.math.PI
import kotlin.math.min
import kotlin.math.sin
import kotlin.random.Random

private const val SWEEP_PERIOD_MS = 12_000
private const val DRIFT_PERIOD_MS = 20_000
private const val PING_PERIOD_MS = 2_000
private val GridStep = 40.dp
private val BracketArm = 24.dp
private val BracketInset = 12.dp
private val EaseOut = CubicBezierEasing(0.2f, 0.8f, 0.2f, 1f)

/**
 * The main menu's decorative backdrop: a dark tactical map (grid, contour lines, elevation rings,
 * faint markers) with a slow radar sweep and screen-corner brackets. Carries no information and is
 * hidden from accessibility services. Still when [animate] is false.
 */
@Composable
fun TacticalMapBackground(modifier: Modifier = Modifier, animate: Boolean = !rememberReducedMotion()) {
    val sweep: State<Float>
    val drift: State<Float>
    val ping: State<Float>
    if (animate) {
        val transition = rememberInfiniteTransition(label = "tactical-map")
        sweep = transition.animateFloat(
            initialValue = 0f,
            targetValue = 360f,
            animationSpec = infiniteRepeatable(tween(SWEEP_PERIOD_MS, easing = LinearEasing)),
            label = "sweep",
        )
        drift = transition.animateFloat(
            initialValue = 0f,
            targetValue = 1f,
            animationSpec = infiniteRepeatable(tween(DRIFT_PERIOD_MS, easing = LinearEasing), RepeatMode.Reverse),
            label = "drift",
        )
        ping = transition.animateFloat(
            initialValue = 0f,
            targetValue = 1f,
            animationSpec = infiniteRepeatable(tween(PING_PERIOD_MS, easing = EaseOut)),
            label = "ping",
        )
    } else {
        sweep = remember { mutableFloatStateOf(300f) }
        drift = remember { mutableFloatStateOf(0.5f) }
        ping = remember { mutableFloatStateOf(1f) }
    }

    val colors = MapColors(
        base = MaterialTheme.colorScheme.background,
        void = MaterialTheme.colorScheme.surfaceContainerLowest,
        deep = MaterialTheme.colorScheme.primaryContainer,
        signal = ThePreyTheme.colors.signal,
        dim = MaterialTheme.colorScheme.secondary,
    )
    val density = LocalDensity.current
    val layoutDirection = LocalLayoutDirection.current
    val insets = WindowInsets.safeDrawing
    val bracketInsets = with(density) {
        val inset = BracketInset.toPx()
        BracketInsets(
            left = insets.getLeft(density, layoutDirection) + inset,
            top = insets.getTop(density) + inset,
            right = insets.getRight(density, layoutDirection) + inset,
            bottom = insets.getBottom(density) + inset,
        )
    }

    Box(
        modifier
            .clearAndSetSemantics {}
            .drawWithCache {
                val contours = contourPaths(size)
                val gridStep = GridStep.toPx()
                val stroke = 1.dp.toPx()
                val arm = BracketArm.toPx()
                onDrawBehind {
                    drawRect(Brush.radialGradient(listOf(colors.base, colors.void), center = center, radius = size.maxDimension * 0.75f))
                    drawGrid(gridStep, colors.deep.copy(alpha = 0.45f), stroke)
                    drawRings(colors, stroke)
                    translate(left = (drift.value - 0.5f) * gridStep) {
                        contours.forEach { drawPath(it, colors.signal.copy(alpha = 0.07f), style = Stroke(stroke)) }
                    }
                    drawSweep(sweep.value, colors)
                    drawMarkers(ping.value, colors, stroke)
                    drawBrackets(bracketInsets, arm, colors.dim, 2.dp.toPx())
                }
            },
    )
}

private class MapColors(val base: Color, val void: Color, val deep: Color, val signal: Color, val dim: Color)

private class BracketInsets(val left: Float, val top: Float, val right: Float, val bottom: Float)

/** Where the elevation rings and the sweep are centred: upper right, like the mockup. */
private fun DrawScope.ringCentre() = Offset(size.width * 0.78f, size.height * 0.36f)

private fun DrawScope.drawGrid(step: Float, color: Color, stroke: Float) {
    var x = step
    while (x < size.width) {
        drawLine(color, Offset(x, 0f), Offset(x, size.height), stroke)
        x += step
    }
    var y = step
    while (y < size.height) {
        drawLine(color, Offset(0f, y), Offset(size.width, y), stroke)
        y += step
    }
}

private fun DrawScope.drawRings(colors: MapColors, stroke: Float) {
    val centre = ringCentre()
    val unit = min(size.width, size.height)
    listOf(0.16f, 0.32f, 0.5f, 0.7f, 0.92f).forEachIndexed { i, r ->
        val radius = unit * r
        val alpha = 0.10f - i * 0.012f
        drawOval(
            color = colors.signal.copy(alpha = alpha),
            topLeft = Offset(centre.x - radius * 1.25f, centre.y - radius),
            size = Size(radius * 2.5f, radius * 2f),
            style = Stroke(stroke),
        )
    }
}

/** Gently wavy horizontal contour lines from a fixed seed, so the map looks the same every time. */
private fun contourPaths(size: Size): List<Path> {
    val random = Random(214)
    return List(5) { line ->
        val baseline = size.height * (0.52f + line * 0.11f)
        val amplitude = size.height * (0.02f + random.nextFloat() * 0.03f)
        val wavelength = size.width * (0.6f + random.nextFloat() * 0.6f)
        val phase = random.nextFloat() * 2f * PI.toFloat()
        Path().apply {
            var x = -size.width * 0.1f
            moveTo(x, baseline)
            while (x <= size.width * 1.1f) {
                lineTo(x, baseline + amplitude * sin(2f * PI.toFloat() * x / wavelength + phase))
                x += 8f
            }
        }
    }
}

private fun DrawScope.drawSweep(angle: Float, colors: MapColors) {
    val centre = ringCentre()
    val radius = size.maxDimension
    rotate(degrees = angle, pivot = centre) {
        drawCircle(
            brush = Brush.sweepGradient(
                0f to Color.Transparent,
                0.80f to Color.Transparent,
                1f to colors.signal.copy(alpha = 0.09f),
                center = centre,
            ),
            radius = radius,
            center = centre,
        )
        // The sweep's leading edge sits at 0°/360° of the gradient: pointing right before rotation.
        drawLine(colors.signal.copy(alpha = 0.16f), centre, Offset(centre.x + radius, centre.y), 1.dp.toPx())
    }
}

private fun DrawScope.drawMarkers(ping: Float, colors: MapColors, stroke: Float) {
    val marker = colors.signal.copy(alpha = 0.14f)
    val unit = 1.dp.toPx()

    // Objective: crossed circle, lower left.
    val objective = Offset(size.width * 0.3f, size.height * 0.79f)
    drawCircle(marker, radius = 22 * unit, center = objective, style = Stroke(stroke * 2))
    val d = 15 * unit
    drawLine(marker, objective + Offset(-d, -d), objective + Offset(d, d), stroke * 2, StrokeCap.Round)
    drawLine(marker, objective + Offset(-d, d), objective + Offset(d, -d), stroke * 2, StrokeCap.Round)

    // Contact: triangle with a ping ring, inside the elevation rings.
    val contact = Offset(size.width * 0.84f, size.height * 0.34f)
    val t = 7 * unit
    drawPath(
        Path().apply {
            moveTo(contact.x, contact.y - t)
            lineTo(contact.x + t, contact.y + t)
            lineTo(contact.x - t, contact.y + t)
            close()
        },
        marker,
    )
    drawCircle(
        colors.signal.copy(alpha = 0.12f * (1f - ping)),
        radius = t + 18 * unit * ping,
        center = contact,
        style = Stroke(stroke),
    )

    // Waypoint: small diamond, right edge.
    val waypoint = Offset(size.width * 0.9f, size.height * 0.81f)
    val w = 5 * unit
    drawPath(
        Path().apply {
            moveTo(waypoint.x, waypoint.y - w)
            lineTo(waypoint.x + w, waypoint.y)
            lineTo(waypoint.x, waypoint.y + w)
            lineTo(waypoint.x - w, waypoint.y)
            close()
        },
        marker,
    )
}

private fun DrawScope.drawBrackets(insets: BracketInsets, arm: Float, color: Color, stroke: Float) {
    val left = insets.left
    val top = insets.top
    val right = size.width - insets.right
    val bottom = size.height - insets.bottom
    fun corner(x: Float, y: Float, dx: Float, dy: Float) {
        drawLine(color, Offset(x, y), Offset(x + dx * arm, y), stroke, StrokeCap.Square)
        drawLine(color, Offset(x, y), Offset(x, y + dy * arm), stroke, StrokeCap.Square)
    }
    corner(left, top, 1f, 1f)
    corner(right, top, -1f, 1f)
    corner(left, bottom, 1f, -1f)
    corner(right, bottom, -1f, -1f)
}

@Preview(widthDp = 360, heightDp = 780)
@Composable
private fun TacticalMapBackgroundPreview() {
    ThePreyTheme { TacticalMapBackground(animate = false, modifier = Modifier.fillMaxSize()) }
}
