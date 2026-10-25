package nl.hexmaster.theprey.ui.playfields

import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.layout.widthIn
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.remember
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.drawBehind
import androidx.compose.ui.geometry.Size
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.LocalConfiguration
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.tooling.preview.Preview
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import nl.hexmaster.theprey.R
import nl.hexmaster.theprey.data.location.GeoPosition
import nl.hexmaster.theprey.ui.components.PrimaryButton
import nl.hexmaster.theprey.ui.components.formatPosition
import nl.hexmaster.theprey.ui.theme.ThePreyTheme
import java.time.LocalDate
import java.time.format.DateTimeFormatter
import java.time.format.FormatStyle

/**
 * One playfield in a list: name, visibility pill, last-updated date and, when known, the centre
 * coordinates. Shows a small spinner while [busy] (a delete is in flight), and the selected-item
 * treatment while [selected] (playfield selection screen).
 */
@Composable
fun PlayFieldRow(item: PlayFieldItem, modifier: Modifier = Modifier, busy: Boolean = false, selected: Boolean = false) {
    val locale = LocalConfiguration.current.locales[0]
    val dateFormat = remember(locale) { DateTimeFormatter.ofLocalizedDate(FormatStyle.MEDIUM).withLocale(locale) }
    val accent = MaterialTheme.colorScheme.primary
    Row(
        modifier
            .fillMaxWidth()
            .then(
                if (selected) {
                    // Selected list item: accent tint behind the row and a 3 dp accent bar on the leading edge.
                    Modifier.drawBehind {
                        drawRect(accent.copy(alpha = 0.12f))
                        drawRect(accent, size = Size(3.dp.toPx(), size.height))
                    }
                } else {
                    Modifier
                },
            )
            .padding(horizontal = 18.dp, vertical = 12.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Column(Modifier.weight(1f)) {
            Text(
                text = item.name,
                style = MaterialTheme.typography.titleMedium,
                color = MaterialTheme.colorScheme.onSurface,
                maxLines = 2,
                overflow = TextOverflow.Ellipsis,
            )
            item.lastUpdated?.let { date ->
                Spacer(Modifier.height(4.dp))
                Text(
                    text = stringResource(R.string.playfields_updated_on, date.format(dateFormat)),
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )
            }
            item.center?.let { center ->
                val (latitude, longitude) = formatPosition(center, locale)
                Spacer(Modifier.height(2.dp))
                Text(
                    text = stringResource(
                        R.string.playfields_center,
                        latitude.degrees,
                        latitude.hemisphere,
                        longitude.degrees,
                        longitude.hemisphere,
                    ),
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )
            }
        }
        if (busy) {
            val deleting = stringResource(R.string.playfields_deleting)
            CircularProgressIndicator(
                modifier = Modifier.size(18.dp).semantics { contentDescription = deleting },
                color = MaterialTheme.colorScheme.onSurfaceVariant,
                strokeWidth = 2.dp,
            )
            Spacer(Modifier.width(12.dp))
        }
        VisibilityPill(isPublic = item.isPublic)
    }
}

/** Status pill: Signal tint for public, neutral (Soft text, Line border, no fill) for private. */
@Composable
private fun VisibilityPill(isPublic: Boolean) {
    val color = if (isPublic) MaterialTheme.colorScheme.primary else MaterialTheme.colorScheme.onSurfaceVariant
    val fill = if (isPublic) color.copy(alpha = 0.12f) else Color.Transparent
    val border = if (isPublic) color.copy(alpha = 0.45f) else MaterialTheme.colorScheme.outline
    Row(
        Modifier
            .height(28.dp)
            .background(fill, CircleShape)
            .border(BorderStroke(1.dp, border), CircleShape)
            .padding(horizontal = 10.dp),
        verticalAlignment = Alignment.CenterVertically,
        horizontalArrangement = Arrangement.spacedBy(6.dp),
    ) {
        Box(Modifier.size(7.dp).background(color, CircleShape))
        Text(
            text = stringResource(if (isPublic) R.string.playfields_badge_public else R.string.playfields_badge_private)
                .uppercase(),
            style = MaterialTheme.typography.labelSmall.copy(fontSize = 11.5.sp),
            color = color,
            maxLines = 1,
        )
    }
}

/** Separator between list rows (design system: `border-subtle`). */
@Composable
fun RowSeparator() {
    HorizontalDivider(thickness = 1.dp, color = MaterialTheme.colorScheme.outlineVariant)
}

/**
 * A centred screen state: a spinner while [loading], otherwise [text] with an optional action
 * (e.g. Retry).
 */
@Composable
fun MessageState(
    text: String?,
    modifier: Modifier = Modifier,
    loading: Boolean = false,
    actionLabel: String? = null,
    onAction: (() -> Unit)? = null,
) {
    Box(modifier.fillMaxSize().padding(24.dp), contentAlignment = Alignment.Center) {
        Column(horizontalAlignment = Alignment.CenterHorizontally, modifier = Modifier.widthIn(max = 420.dp)) {
            if (loading) {
                CircularProgressIndicator(color = MaterialTheme.colorScheme.primary)
            }
            if (text != null) {
                if (loading) Spacer(Modifier.height(16.dp))
                Text(
                    text = text,
                    style = MaterialTheme.typography.bodyMedium,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                    textAlign = TextAlign.Center,
                )
            }
            if (actionLabel != null && onAction != null) {
                Spacer(Modifier.height(20.dp))
                PrimaryButton(text = actionLabel, onClick = onAction, modifier = Modifier.widthIn(max = 240.dp))
            }
        }
    }
}

internal val previewItems = listOf(
    PlayFieldItem("1", "Vondelpark", isPublic = true, LocalDate.of(2026, 10, 20), GeoPosition(52.358, 4.8686)),
    PlayFieldItem("2", "Backyard", isPublic = false, LocalDate.of(2026, 9, 2), center = null),
    PlayFieldItem("3", "Westerpark loop", isPublic = false, LocalDate.of(2026, 8, 14), GeoPosition(52.3866, 4.8737)),
)

@Composable
private fun RowsPreviewContent() {
    ThePreyTheme {
        Column(Modifier.background(MaterialTheme.colorScheme.surface)) {
            PlayFieldRow(previewItems[0])
            RowSeparator()
            PlayFieldRow(previewItems[1], busy = true)
            RowSeparator()
            PlayFieldRow(previewItems[2], selected = true)
            RowSeparator()
            MessageState(
                text = stringResource(R.string.playfields_load_error),
                actionLabel = stringResource(R.string.playfields_retry),
                onAction = {},
                modifier = Modifier.height(240.dp),
            )
        }
    }
}

@Preview(name = "Rows (English)")
@Composable
private fun PlayFieldRowPreview() = RowsPreviewContent()

@Preview(name = "Rows (Dutch)", locale = "nl")
@Composable
private fun PlayFieldRowDutchPreview() = RowsPreviewContent()

@Preview(name = "Selected")
@Composable
private fun PlayFieldRowSelectedPreview() {
    ThePreyTheme {
        Column(Modifier.background(MaterialTheme.colorScheme.background)) {
            PlayFieldRow(previewItems[0], selected = true)
            RowSeparator()
            PlayFieldRow(previewItems[1])
        }
    }
}
