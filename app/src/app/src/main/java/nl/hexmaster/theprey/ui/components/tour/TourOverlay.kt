package nl.hexmaster.theprey.ui.components.tour

import androidx.activity.compose.BackHandler
import androidx.compose.animation.core.animateRectAsState
import androidx.compose.animation.core.snap
import androidx.compose.animation.core.tween
import androidx.compose.foundation.Canvas
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.WindowInsets
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.offset
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.safeDrawing
import androidx.compose.foundation.layout.widthIn
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableFloatStateOf
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.geometry.CornerRadius
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.geometry.Rect
import androidx.compose.ui.geometry.Size
import androidx.compose.ui.graphics.BlendMode
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.CompositingStrategy
import androidx.compose.ui.graphics.drawscope.Stroke
import androidx.compose.ui.graphics.graphicsLayer
import androidx.compose.ui.input.pointer.pointerInput
import androidx.compose.ui.layout.onGloballyPositioned
import androidx.compose.ui.layout.onSizeChanged
import androidx.compose.ui.layout.positionInRoot
import androidx.compose.ui.platform.LocalDensity
import androidx.compose.ui.platform.LocalLayoutDirection
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.semantics.LiveRegionMode
import androidx.compose.ui.semantics.clearAndSetSemantics
import androidx.compose.ui.semantics.liveRegion
import androidx.compose.ui.semantics.paneTitle
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.tooling.preview.Preview
import androidx.compose.ui.unit.IntOffset
import androidx.compose.ui.unit.dp
import nl.hexmaster.theprey.R
import nl.hexmaster.theprey.ui.components.PrimaryButton
import nl.hexmaster.theprey.ui.components.SecondaryButton
import nl.hexmaster.theprey.ui.components.rememberReducedMotion
import nl.hexmaster.theprey.ui.theme.ThePreyTheme
import kotlin.math.roundToInt

/** Void at 75 %: dims the screen around the spotlight. */
private const val SCRIM_ALPHA = 0.75f
private const val SPOTLIGHT_MS = 200
private val SpotlightPadding = 8.dp
private val SpotlightCorner = 12.dp
private val CardGap = 16.dp
private val CardMaxWidth = 420.dp

/**
 * A coach-mark tour over the whole screen: a dimmed scrim with a cut-out around the current step's
 * control (found in [targets]) and a card with the step's title and body, a counter and Skip,
 * Back and Next or Done. A step whose control is not laid out gets a centred card. Every touch is
 * consumed, so nothing underneath reacts; system Back skips. The step survives rotation.
 * [onFinish] is called once, with how the tour closed.
 */
@Composable
fun TourOverlay(
    steps: List<TourStep>,
    onFinish: (TourOutcome) -> Unit,
    modifier: Modifier = Modifier,
    targets: TourTargets? = LocalTourTargets.current,
    reducedMotion: Boolean = rememberReducedMotion(),
) {
    var index by rememberSaveable { mutableIntStateOf(0) }
    val state = TourState(steps.size, index.coerceIn(0, steps.lastIndex))
    val step = steps[state.index]
    val density = LocalDensity.current
    val layoutDirection = LocalLayoutDirection.current
    val insets = WindowInsets.safeDrawing

    // The overlay's own origin and size in root coordinates; targets are registered in root coordinates.
    var origin by remember { mutableStateOf(Offset.Zero) }
    var size by remember { mutableStateOf(Size.Zero) }
    var cardHeight by remember { mutableFloatStateOf(0f) }

    BackHandler { onFinish(TourOutcome.Skipped) }

    val screen = Rect(Offset.Zero, size)
    val target = step.targetKey?.let { targets?.get(it) }?.translate(-origin)?.takeIf { it.overlaps(screen) }
    val spotlight = target?.let { TourPlacement.spotlight(it, with(density) { SpotlightPadding.toPx() }, screen) }
    val animated by animateRectAsState(
        targetValue = spotlight ?: Rect.Zero,
        animationSpec = if (reducedMotion) snap() else tween(SPOTLIGHT_MS),
        label = "spotlight",
    )
    val safe = Rect(
        left = insets.getLeft(density, layoutDirection).toFloat(),
        top = insets.getTop(density).toFloat(),
        right = size.width - insets.getRight(density, layoutDirection),
        bottom = size.height - insets.getBottom(density),
    ).let { it.deflate(with(density) { 16.dp.toPx() }).takeIf { r -> r.height > 0 && r.width > 0 } ?: it }
    val cardTop = TourPlacement.cardTop(spotlight, safe, cardHeight, with(density) { CardGap.toPx() })

    val void = ThePreyTheme.colors.void
    val signal = ThePreyTheme.colors.signal
    val title = stringResource(step.title)
    val paneTitle = stringResource(R.string.tour_a11y_pane, state.number, state.stepCount, title)

    Box(
        modifier
            .fillMaxSize()
            .onGloballyPositioned {
                origin = it.positionInRoot()
                size = Size(it.size.width.toFloat(), it.size.height.toFloat())
            },
    ) {
        Canvas(
            Modifier
                .fillMaxSize()
                .graphicsLayer { compositingStrategy = CompositingStrategy.Offscreen }
                // Swallow every touch, so the screen underneath never reacts while the tour is open.
                .pointerInput(Unit) {
                    awaitPointerEventScope {
                        while (true) awaitPointerEvent().changes.forEach { it.consume() }
                    }
                }
                .clearAndSetSemantics {},
        ) {
            drawRect(void.copy(alpha = SCRIM_ALPHA))
            if (spotlight != null) {
                val corner = CornerRadius(SpotlightCorner.toPx())
                drawRoundRect(Color.Black, animated.topLeft, animated.size, corner, blendMode = BlendMode.Clear)
                drawRoundRect(signal, animated.topLeft, animated.size, corner, style = Stroke(2.dp.toPx()))
            }
        }

        Column(
            Modifier
                .align(Alignment.TopCenter)
                .offset { IntOffset(0, cardTop.roundToInt()) }
                .padding(horizontal = 16.dp)
                .widthIn(max = CardMaxWidth)
                .fillMaxWidth()
                .onSizeChanged { cardHeight = it.height.toFloat() }
                .background(MaterialTheme.colorScheme.surface, MaterialTheme.shapes.large)
                .border(1.dp, MaterialTheme.colorScheme.outline, MaterialTheme.shapes.large)
                .padding(20.dp)
                .semantics {
                    this.paneTitle = paneTitle
                    liveRegion = LiveRegionMode.Polite
                },
        ) {
            Text(
                text = stringResource(R.string.tour_counter, state.number, state.stepCount),
                style = MaterialTheme.typography.labelMedium,
                color = MaterialTheme.colorScheme.secondary,
            )
            Spacer(Modifier.height(8.dp))
            Text(text = title, style = MaterialTheme.typography.titleLarge, color = MaterialTheme.colorScheme.onSurface)
            Spacer(Modifier.height(8.dp))
            Text(
                text = stringResource(step.body, *step.bodyArgs.toTypedArray()),
                style = MaterialTheme.typography.bodyMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
            Spacer(Modifier.height(16.dp))
            Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                TextButton(onClick = { onFinish(TourOutcome.Skipped) }) {
                    Text(
                        text = stringResource(R.string.tour_skip).uppercase(),
                        style = MaterialTheme.typography.labelLarge,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                    )
                }
                Spacer(Modifier.weight(1f))
                if (!state.isFirst) {
                    SecondaryButton(text = stringResource(R.string.tour_back), onClick = { index = state.back().index })
                }
                PrimaryButton(
                    text = stringResource(if (state.isLast) R.string.tour_done else R.string.tour_next),
                    onClick = { if (state.isLast) onFinish(TourOutcome.Completed) else index = state.next().index },
                )
            }
        }
    }
}

// ---- Previews ----

private val previewSteps = listOf(
    TourStep(null, R.string.tour_area_intro_title, R.string.tour_area_intro_body),
    TourStep("target", R.string.tour_area_delete_title, R.string.tour_area_delete_body),
    TourStep("target", R.string.tour_area_save_title, R.string.tour_area_save_body),
)

@Composable
private fun OverlayPreview(index: Int, withTarget: Boolean = true) {
    val targets = remember { TourTargets() }
    ThePreyTheme {
        Box(Modifier.fillMaxSize().background(MaterialTheme.colorScheme.background)) {
            if (withTarget) {
                Box(
                    Modifier
                        .align(Alignment.BottomEnd)
                        .padding(24.dp)
                        .background(ThePreyTheme.colors.hunter, MaterialTheme.shapes.large)
                        .padding(28.dp)
                        .onGloballyPositioned { targets.update("target", Rect(it.positionInRoot(), it.size.let { s -> Size(s.width.toFloat(), s.height.toFloat()) })) },
                )
            }
            // rememberSaveable starts on step 1 in previews; slice the list to show a later step.
            TourOverlay(steps = previewSteps.drop(index), onFinish = {}, targets = targets, reducedMotion = true)
        }
    }
}

@Preview(name = "First step, centred", heightDp = 640)
@Composable
private fun FirstStepPreview() = OverlayPreview(index = 0, withTarget = false)

@Preview(name = "Spotlight", heightDp = 640)
@Composable
private fun SpotlightPreview() = OverlayPreview(index = 1)

@Preview(name = "Last step (Dutch)", heightDp = 640, locale = "nl")
@Composable
private fun LastStepDutchPreview() = OverlayPreview(index = 2)
