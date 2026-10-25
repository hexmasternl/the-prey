package nl.hexmaster.theprey.ui.game.hud.common

import android.os.SystemClock
import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableLongStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.LocalConfiguration
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.semantics.clearAndSetSemantics
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import kotlinx.coroutines.delay
import kotlinx.coroutines.isActive
import nl.hexmaster.theprey.R
import nl.hexmaster.theprey.ui.theme.ThePreyTheme
import java.text.NumberFormat

/** Translucent Void behind HUD chrome, so the map stays visible (design system: status bar). */
@Composable
fun hudSurface(): Color = ThePreyTheme.colors.void.copy(alpha = 0.82f)

/**
 * A HUD status pill: [color] fill with a dark ([onColor]) label and a leading dot, or with
 * [filled] false a translucent pill with [color] text. [description] replaces the label for screen readers.
 */
@Composable
fun HudPill(
    text: String,
    color: Color,
    modifier: Modifier = Modifier,
    filled: Boolean = true,
    onColor: Color = ThePreyTheme.colors.void,
    description: String? = null,
) {
    val textColor = if (filled) onColor else color
    Row(
        modifier
            .height(32.dp)
            .background(if (filled) color else hudSurface(), CircleShape)
            .border(BorderStroke(1.dp, if (filled) color else color.copy(alpha = 0.5f)), CircleShape)
            .padding(horizontal = 12.dp)
            .then(if (description != null) Modifier.clearAndSetSemantics { contentDescription = description } else Modifier),
        verticalAlignment = Alignment.CenterVertically,
        horizontalArrangement = Arrangement.spacedBy(7.dp),
    ) {
        Box(Modifier.size(7.dp).background(textColor, CircleShape))
        Text(
            text = text.uppercase(),
            style = MaterialTheme.typography.labelMedium.copy(fontSize = 12.sp, letterSpacing = 1.2.sp),
            color = textColor,
            maxLines = 1,
        )
    }
}

/** A HUD stat tile: UPPERCASE label in Soft, value in Special Elite. */
@Composable
fun HudTile(
    label: String,
    value: String,
    modifier: Modifier = Modifier,
    valueColor: Color = MaterialTheme.colorScheme.primary,
    content: @Composable () -> Unit = {},
) {
    Column(
        modifier
            .background(hudSurface(), MaterialTheme.shapes.medium)
            .border(BorderStroke(1.dp, MaterialTheme.colorScheme.outlineVariant), MaterialTheme.shapes.medium)
            .padding(horizontal = 14.dp, vertical = 12.dp),
    ) {
        Text(
            text = label.uppercase(),
            style = MaterialTheme.typography.labelSmall.copy(letterSpacing = 1.5.sp),
            color = MaterialTheme.colorScheme.onSurfaceVariant,
            maxLines = 2,
            overflow = TextOverflow.Ellipsis,
        )
        Spacer(Modifier.height(6.dp))
        Text(text = value, style = MaterialTheme.typography.headlineMedium, color = valueColor, maxLines = 1)
        content()
    }
}

/** A distance as whole metres up to 999 m, then kilometres with one decimal in the device locale. */
@Composable
fun formatDistance(meters: Int?): String {
    if (meters == null) return stringResource(R.string.hud_no_value)
    if (meters < 1_000) return stringResource(R.string.hud_meters, meters)
    val locale = LocalConfiguration.current.locales[0]
    val format = remember(locale) {
        NumberFormat.getNumberInstance(locale).apply {
            minimumFractionDigits = 1
            maximumFractionDigits = 1
        }
    }
    return stringResource(R.string.hud_kilometers, format.format(meters / 1_000.0))
}

/** The device's elapsed realtime, refreshed every [periodMillis]; drives the HUD countdowns. */
@Composable
fun rememberElapsedTicker(periodMillis: Long = 1_000L): Long {
    var now by remember { mutableLongStateOf(SystemClock.elapsedRealtime()) }
    LaunchedEffect(periodMillis) {
        while (isActive) {
            // Align to the period so the counters change together.
            delay(periodMillis - (SystemClock.elapsedRealtime() % periodMillis))
            now = SystemClock.elapsedRealtime()
        }
    }
    return now
}
