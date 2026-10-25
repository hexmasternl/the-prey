package nl.hexmaster.theprey.ui.splash

import androidx.compose.foundation.Canvas
import androidx.compose.foundation.background
import androidx.compose.foundation.gestures.detectTapGestures
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.WindowInsets
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.offset
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.safeDrawing
import androidx.compose.foundation.layout.windowInsetsPadding
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableLongStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberUpdatedState
import androidx.compose.runtime.setValue
import androidx.compose.runtime.withFrameMillis
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.drawWithContent
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.geometry.Size
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.StrokeCap
import androidx.compose.ui.graphics.drawscope.DrawScope
import androidx.compose.ui.graphics.drawscope.Stroke
import androidx.compose.ui.graphics.drawscope.rotate
import androidx.compose.ui.graphics.drawscope.scale
import androidx.compose.ui.graphics.drawscope.translate
import androidx.compose.ui.graphics.graphicsLayer
import androidx.compose.ui.input.pointer.pointerInput
import androidx.compose.ui.platform.LocalDensity
import androidx.compose.ui.res.stringArrayResource
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.semantics.clearAndSetSemantics
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.text.SpanStyle
import androidx.compose.ui.text.TextLayoutResult
import androidx.compose.ui.text.buildAnnotatedString
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.text.withStyle
import androidx.compose.ui.tooling.preview.Preview
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import kotlinx.coroutines.delay
import nl.hexmaster.theprey.BuildConfig
import nl.hexmaster.theprey.R
import nl.hexmaster.theprey.ui.components.PREY_MARK_BOX_RATIO
import nl.hexmaster.theprey.ui.components.PreyMark
import nl.hexmaster.theprey.ui.components.rememberReducedMotion
import nl.hexmaster.theprey.ui.theme.ThePreyTheme

/**
 * Size of the system splash icon box (core-splashscreen / Android 12+ without an icon background).
 * The hand-off frame draws `ic_prey_mark` in a box of exactly this size, centred in the full window,
 * so the mark stays put when the system splash is replaced by this composable.
 */
private val SplashIconBox = 288.dp
private val MarkSize = SplashIconBox / PREY_MARK_BOX_RATIO

/** How far the mark rises to make room for the title. */
private val MarkLift = 96.dp
private val TitleOffset = 64.dp
private val GridStep = 40.dp
private val BracketArm = 28.dp
private val BracketInset = 16.dp

/**
 * The cold-start launch sequence: continues from the system splash, plays [LaunchTimeline] once
 * (or shows its final frame for a second when animations are off) and then calls [onFinished].
 * A tap anywhere skips straight to [onFinished]. Never waits on the network.
 *
 * The clock only runs once [started] is true (the system splash is gone); until then the hand-off
 * frame is held, because Compose keeps producing frames underneath a held system splash.
 */
@Composable
fun LaunchSequence(started: Boolean, onFinished: () -> Unit, modifier: Modifier = Modifier) {
    val reducedMotion = rememberReducedMotion()
    val title = "${stringResource(R.string.brand_word_the)} ${stringResource(R.string.brand_word_prey)}".uppercase()
    val finish by rememberUpdatedState(onFinished)
    var elapsedMs by remember { mutableLongStateOf(0L) }

    LaunchedEffect(reducedMotion, started) {
        if (!started) return@LaunchedEffect
        if (reducedMotion) {
            delay(LaunchTimeline.REDUCED_MOTION_HOLD_MS)
        } else {
            val start = withFrameMillis { it }
            while (elapsedMs < LaunchTimeline.TOTAL_MS) {
                elapsedMs = withFrameMillis { it } - start
            }
        }
        finish()
    }

    val frame = if (reducedMotion) LaunchTimeline.finalFrame(title.length) else LaunchTimeline.frameAt(elapsedMs, title.length)
    LaunchSequenceContent(
        frame = frame,
        title = title,
        versionName = BuildConfig.VERSION_NAME,
        modifier = modifier.pointerInput(Unit) { detectTapGestures { finish() } },
    )
}

/** Stateless rendering of one [LaunchFrame]. */
@Composable
fun LaunchSequenceContent(frame: LaunchFrame, title: String, versionName: String, modifier: Modifier = Modifier) {
    val appName = stringResource(R.string.app_name)
    val density = LocalDensity.current
    val liftPx = with(density) { MarkLift.toPx() }

    Box(
        modifier
            .fillMaxSize()
            .background(ThePreyTheme.colors.void)
            // TalkBack: one announcement, "The Prey"; everything inside is decorative.
            .semantics(mergeDescendants = true) { contentDescription = appName },
    ) {
        LaunchBackdrop(frame, Modifier.fillMaxSize().clearAndSetSemantics {})

        PreyMark(
            markSize = MarkSize,
            contentDescription = null,
            modifier = Modifier
                .align(Alignment.Center)
                .graphicsLayer {
                    translationY = -frame.markLift * liftPx
                    scaleX = frame.markScale
                    scaleY = frame.markScale
                },
        )

        Column(
            modifier = Modifier
                .align(Alignment.Center)
                .offset(y = TitleOffset)
                .padding(horizontal = 24.dp)
                .clearAndSetSemantics {},
            horizontalAlignment = Alignment.CenterHorizontally,
        ) {
            TypedTitle(title, frame.titleChars, frame.cursorVisible)
            Spacer(Modifier.height(12.dp))
            Tagline(
                Modifier.graphicsLayer {
                    alpha = frame.taglineAlpha
                    translationY = frame.taglineSlide * 12.dp.toPx()
                },
            )
        }

        StatusBlock(
            frame = frame,
            versionName = versionName,
            modifier = Modifier
                .align(Alignment.BottomCenter)
                .windowInsetsPadding(WindowInsets.safeDrawing)
                .padding(horizontal = 24.dp, vertical = 32.dp)
                .clearAndSetSemantics {},
        )
    }
}

/** Grid, radar sweep, ping rings, head glow and corner brackets in one Canvas. */
@Composable
private fun LaunchBackdrop(frame: LaunchFrame, modifier: Modifier = Modifier) {
    val colors = ThePreyTheme.colors
    val gridColor = MaterialTheme.colorScheme.primaryContainer
    val density = LocalDensity.current
    val insets = WindowInsets.safeDrawing

    // Everything that allocates is built once; the draw lambda only reads the frame.
    val sweepBrush = remember(colors) {
        Brush.sweepGradient(
            0f to Color.Transparent,
            0.72f to Color.Transparent,
            1f to colors.signalGlow,
            center = Offset.Zero,
        )
    }
    val markPx = with(density) { MarkSize.toPx() }
    val headBrush = remember(colors, markPx) {
        Brush.radialGradient(
            0f to colors.hunter.copy(alpha = 0.55f),
            1f to Color.Transparent,
            center = Offset.Zero,
            radius = markPx * 0.42f,
        )
    }
    val thin = remember(density) { Stroke(width = with(density) { 1.dp.toPx() }) }
    val bracketStroke = remember(density) { Stroke(width = with(density) { 2.dp.toPx() }, cap = StrokeCap.Square) }
    val liftPx = with(density) { MarkLift.toPx() }

    Canvas(modifier) {
        val step = GridStep.toPx()
        val gridAlpha = 0.35f * frame.gridAlpha
        if (gridAlpha > 0f) {
            var x = (size.width / 2f) % step
            while (x < size.width) {
                drawLine(gridColor, Offset(x, 0f), Offset(x, size.height), thin.width, alpha = gridAlpha)
                x += step
            }
            var y = (size.height / 2f) % step
            while (y < size.height) {
                drawLine(gridColor, Offset(0f, y), Offset(size.width, y), thin.width, alpha = gridAlpha)
                y += step
            }
        }

        val markCenter = Offset(size.width / 2f, size.height / 2f - frame.markLift * liftPx)
        val radarRadius = markPx * 0.95f * frame.markScale
        translate(markCenter.x, markCenter.y) {
            if (frame.sweepAlpha > 0f) {
                drawCircle(gridColor, radius = radarRadius, center = Offset.Zero, style = thin, alpha = frame.sweepAlpha)
                // Sweep gradients start at 3 o'clock; -90° puts angle 0 at 12 o'clock.
                rotate(frame.sweepAngle - 90f, pivot = Offset.Zero) {
                    drawCircle(sweepBrush, radius = radarRadius, center = Offset.Zero, alpha = frame.sweepAlpha)
                }
            }
            for (ping in frame.pings) {
                drawCircle(
                    colors.signal,
                    radius = radarRadius * (0.45f + 0.75f * ping.radius),
                    center = Offset.Zero,
                    style = thin,
                    alpha = 0.6f * ping.alpha,
                )
            }
            if (frame.headFlash > 0f) {
                scale(frame.markScale, pivot = Offset.Zero) {
                    drawCircle(headBrush, radius = markPx * 0.42f, center = Offset.Zero, alpha = frame.headFlash)
                    drawCircle(colors.hunter, radius = markPx * 0.36f, center = Offset.Zero, style = thin, alpha = frame.headFlash)
                }
            }
        }

        if (frame.bracketProgress > 0f) {
            val inset = BracketInset.toPx()
            drawBrackets(
                left = insets.getLeft(this, layoutDirection) + inset,
                top = insets.getTop(this) + inset,
                right = size.width - insets.getRight(this, layoutDirection) - inset,
                bottom = size.height - insets.getBottom(this) - inset,
                arm = BracketArm.toPx() * frame.bracketProgress,
                color = colors.signal,
                stroke = bracketStroke,
                alpha = frame.bracketProgress,
            )
        }
    }
}

private fun DrawScope.drawBrackets(
    left: Float,
    top: Float,
    right: Float,
    bottom: Float,
    arm: Float,
    color: Color,
    stroke: Stroke,
    alpha: Float,
) {
    fun corner(x: Float, y: Float, dx: Float, dy: Float) {
        drawLine(color, Offset(x, y), Offset(x + dx * arm, y), stroke.width, stroke.cap, alpha = alpha)
        drawLine(color, Offset(x, y), Offset(x, y + dy * arm), stroke.width, stroke.cap, alpha = alpha)
    }
    corner(left, top, 1f, 1f)
    corner(right, top, -1f, 1f)
    corner(left, bottom, 1f, -1f)
    corner(right, bottom, -1f, -1f)
}

/**
 * "THE PREY" typed out: the whole title is laid out from the start (untyped letters transparent)
 * so it never shifts while typing, and a block cursor is drawn after the last typed letter.
 */
@Composable
private fun TypedTitle(title: String, visibleChars: Int, cursorVisible: Boolean) {
    val textColor = MaterialTheme.colorScheme.onBackground
    val signal = ThePreyTheme.colors.signal
    val accentFrom = title.indexOf(' ') + 1
    var layout by remember { mutableStateOf<TextLayoutResult?>(null) }
    val text = buildAnnotatedString {
        title.forEachIndexed { index, char ->
            val color = when {
                index >= visibleChars -> Color.Transparent
                index >= accentFrom -> signal
                else -> textColor
            }
            withStyle(SpanStyle(color = color)) { append(char) }
        }
    }
    Text(
        text = text,
        style = MaterialTheme.typography.displayMedium,
        textAlign = TextAlign.Center,
        maxLines = 1,
        onTextLayout = { layout = it },
        modifier = Modifier.drawWithContent {
            drawContent()
            val result = layout
            if (cursorVisible && result != null) {
                val cursor = result.getCursorRect(visibleChars.coerceIn(0, title.length))
                val width = result.size.height * 0.32f
                drawRect(
                    color = signal,
                    topLeft = Offset(cursor.left + width * 0.2f, cursor.top + cursor.height * 0.15f),
                    size = Size(width, cursor.height * 0.7f),
                )
            }
        },
    )
}

/** "STAY HIDDEN. HUNT SMART." with the second word of each half in Signal green. */
@Composable
private fun Tagline(modifier: Modifier = Modifier) {
    val text = MaterialTheme.colorScheme.onBackground
    val accent = ThePreyTheme.colors.signal
    val tagline = buildAnnotatedString {
        withStyle(SpanStyle(color = text)) { append(stringResource(R.string.launch_tagline_part1).uppercase()) }
        append(' ')
        withStyle(SpanStyle(color = accent)) { append(stringResource(R.string.launch_tagline_part2).uppercase()) }
        append(' ')
        withStyle(SpanStyle(color = text)) { append(stringResource(R.string.launch_tagline_part3).uppercase()) }
        append(' ')
        withStyle(SpanStyle(color = accent)) { append(stringResource(R.string.launch_tagline_part4).uppercase()) }
    }
    Text(text = tagline, style = MaterialTheme.typography.headlineSmall, textAlign = TextAlign.Center, modifier = modifier)
}

/** UPPERCASE boot status line plus the coordinate/version readout. */
@Composable
private fun StatusBlock(frame: LaunchFrame, versionName: String, modifier: Modifier = Modifier) {
    val messages = stringArrayResource(R.array.launch_status_messages)
    val index = frame.statusIndex.coerceAtMost(messages.lastIndex)
    val isLast = index == messages.lastIndex
    Column(
        modifier = modifier.fillMaxWidth().graphicsLayer { alpha = frame.readoutAlpha },
        horizontalAlignment = Alignment.CenterHorizontally,
        verticalArrangement = Arrangement.spacedBy(8.dp),
    ) {
        Text(
            // Keep the line's height while there is no message yet, so nothing jumps.
            text = if (index >= 0) "> ${messages[index].uppercase()}" else " ",
            style = MaterialTheme.typography.labelSmall.copy(letterSpacing = 2.sp),
            color = if (isLast) ThePreyTheme.colors.signal else MaterialTheme.colorScheme.onSurfaceVariant,
        )
        Text(
            text = stringResource(R.string.launch_readout, versionName).uppercase(),
            style = MaterialTheme.typography.labelSmall.copy(letterSpacing = 2.sp),
            color = MaterialTheme.colorScheme.secondary,
        )
    }
}

@Preview(widthDp = 411, heightDp = 891)
@Composable
private fun LaunchHandOffPreview() = LaunchPreview(0)

@Preview(widthDp = 411, heightDp = 891)
@Composable
private fun LaunchAcquiredPreview() = LaunchPreview(560)

@Preview(widthDp = 411, heightDp = 891)
@Composable
private fun LaunchTypingPreview() = LaunchPreview(1000)

@Preview(widthDp = 411, heightDp = 891)
@Composable
private fun LaunchTaglinePreview() = LaunchPreview(1600)

@Preview(widthDp = 411, heightDp = 891)
@Composable
private fun LaunchFinalPreview() {
    ThePreyTheme { LaunchSequenceContent(LaunchTimeline.finalFrame(), "THE PREY", "0.1.0") }
}

@Composable
private fun LaunchPreview(elapsedMs: Long) {
    ThePreyTheme { LaunchSequenceContent(LaunchTimeline.frameAt(elapsedMs), "THE PREY", "0.1.0") }
}
