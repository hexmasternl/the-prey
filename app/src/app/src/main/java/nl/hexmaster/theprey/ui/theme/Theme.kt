package nl.hexmaster.theprey.ui.theme

import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.darkColorScheme
import androidx.compose.runtime.Composable
import androidx.compose.runtime.CompositionLocalProvider
import androidx.compose.runtime.Immutable
import androidx.compose.runtime.ReadOnlyComposable
import androidx.compose.runtime.staticCompositionLocalOf
import androidx.compose.ui.graphics.Color

/** Game-specific colors that have no Material 3 role. */
@Immutable
data class ThePreyColors(
    val signal: Color,
    val signalGlow: Color,
    val hunter: Color,
    val hunterDim: Color,
    val prey: Color,
    val caution: Color,
    /** Orange between [caution] and [hunter]: the prey HUD's "Warning" band. */
    val alert: Color,
    val info: Color,
    val void: Color,
    val textGhost: Color,
)

private val DarkThePreyColors = ThePreyColors(
    signal = Signal,
    signalGlow = SignalGlow,
    hunter = Hunter,
    hunterDim = HunterDim,
    prey = Prey,
    caution = Caution,
    alert = Alert,
    info = Info,
    void = BgVoid,
    textGhost = TextGhost,
)

private val DarkColorScheme = darkColorScheme(
    primary = Signal,
    onPrimary = BgVoid,
    primaryContainer = SignalDeep,
    onPrimaryContainer = Text,
    secondary = SignalDim,
    onSecondary = BgVoid,
    tertiary = Info,
    onTertiary = BgVoid,
    error = Hunter,
    onError = BgVoid,
    errorContainer = HunterDim,
    onErrorContainer = Text,
    background = BgBase,
    onBackground = Text,
    surface = BgSurface,
    onSurface = Text,
    surfaceVariant = BgSurface2,
    onSurfaceVariant = TextSoft,
    surfaceContainerLowest = BgVoid,
    outline = Line,
    outlineVariant = LineSoft,
)

private val LocalThePreyColors = staticCompositionLocalOf { DarkThePreyColors }

/**
 * The Prey's tactical, dark-first theme. Dark is the primary identity, so it is applied
 * regardless of the system setting until a daylight palette is designed.
 */
@Composable
fun ThePreyTheme(content: @Composable () -> Unit) {
    CompositionLocalProvider(LocalThePreyColors provides DarkThePreyColors) {
        MaterialTheme(
            colorScheme = DarkColorScheme,
            typography = ThePreyTypography,
            content = content,
        )
    }
}

object ThePreyTheme {
    val colors: ThePreyColors
        @Composable
        @ReadOnlyComposable
        get() = LocalThePreyColors.current
}
