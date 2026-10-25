package nl.hexmaster.theprey.ui.playfields.area

import androidx.activity.compose.BackHandler
import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.navigationBarsPadding
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.statusBarsPadding
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.layout.onSizeChanged
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.platform.LocalDensity
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.TextStyle
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.tooling.preview.Preview
import androidx.compose.ui.unit.Dp
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import kotlinx.coroutines.flow.firstOrNull
import kotlinx.coroutines.withTimeoutOrNull
import nl.hexmaster.theprey.R
import nl.hexmaster.theprey.data.location.GeoPosition
import nl.hexmaster.theprey.data.location.LocationAccess
import nl.hexmaster.theprey.data.location.LocationSource
import nl.hexmaster.theprey.data.location.readLocationAccess
import nl.hexmaster.theprey.ui.theme.BodyFontFamily
import nl.hexmaster.theprey.ui.theme.ThePreyTheme

/** How long the area screen waits for a first device fix before staying on the world view. */
private const val LOCATE_TIMEOUT_MS = 5_000L

/** Floating chrome over the map: Void at 82 % (design system). */
private const val CHROME_ALPHA = 0.82f

private val BarButtonHeight = 48.dp

/**
 * The playfield area, full screen: a map edge to edge with the actions floating at the bottom.
 * Editable: Cancel, Erase All and Save (which hands the points to the details draft); read-only:
 * Close only. Cancel, Close, the back chip and system Back all leave without changing the details.
 * Location permission is never asked for here: the device position is only used when it is
 * already granted.
 */
@Composable
fun PlayFieldAreaScreen(
    viewModel: PlayFieldAreaViewModel,
    locationSource: LocationSource,
    onClose: () -> Unit,
    modifier: Modifier = Modifier,
) {
    val uiState by viewModel.uiState.collectAsStateWithLifecycle()
    val context = LocalContext.current
    val density = LocalDensity.current
    var chromeHeight by remember { mutableStateOf(0.dp) }

    BackHandler(onBack = onClose)

    Box(modifier.fillMaxSize().background(ThePreyTheme.colors.void)) {
        PlayFieldMap(
            points = uiState.points,
            selectedIndex = uiState.selectedIndex,
            editable = uiState.editable,
            onMapTapped = viewModel::onMapTapped,
            onPointTapped = viewModel::onPointTapped,
            onDragSelected = viewModel::onDragSelected,
            locateDevice = {
                if (readLocationAccess(context) == LocationAccess.Missing) {
                    null
                } else {
                    withTimeoutOrNull(LOCATE_TIMEOUT_MS) { locationSource.positions().firstOrNull() }
                }
            },
            contentPadding = PaddingValues(bottom = chromeHeight),
            modifier = Modifier.fillMaxSize(),
        )

        BackChip(onClick = onClose, modifier = Modifier.align(Alignment.TopStart).statusBarsPadding().padding(12.dp))

        Column(
            Modifier
                .align(Alignment.BottomCenter)
                .fillMaxWidth()
                .onSizeChanged { chromeHeight = with(density) { it.height.toDp() } },
        ) {
            MapAttribution(Modifier.align(Alignment.End).padding(end = 8.dp, bottom = 4.dp))
            AreaActionBar(
                editable = uiState.editable,
                canErase = uiState.canErase,
                canSave = uiState.canSave,
                onCancel = onClose,
                onEraseAll = viewModel::eraseAll,
                onSave = { if (viewModel.save()) onClose() },
            )
        }
    }
}

/** The tile licence's credit line, always readable above the bar. */
@Composable
private fun MapAttribution(modifier: Modifier = Modifier) {
    Text(
        text = stringResource(R.string.playfield_area_attribution),
        style = MaterialTheme.typography.labelSmall.copy(fontSize = 10.sp),
        color = MaterialTheme.colorScheme.onSurfaceVariant,
        maxLines = 1,
        modifier = modifier
            .background(ThePreyTheme.colors.void.copy(alpha = CHROME_ALPHA), MaterialTheme.shapes.extraSmall)
            .padding(horizontal = 6.dp, vertical = 2.dp),
    )
}

@Composable
private fun BackChip(onClick: () -> Unit, modifier: Modifier = Modifier) {
    IconButton(
        onClick = onClick,
        modifier = modifier
            .size(44.dp)
            .background(ThePreyTheme.colors.void.copy(alpha = CHROME_ALPHA), CircleShape)
            .border(BorderStroke(1.dp, MaterialTheme.colorScheme.outlineVariant), CircleShape),
    ) {
        Icon(
            painter = painterResource(R.drawable.ic_arrow_back),
            contentDescription = stringResource(R.string.playfield_area_back),
            tint = MaterialTheme.colorScheme.onBackground,
        )
    }
}

/**
 * The translucent bottom bar: Cancel (neutral), Erase All (Hunter) and Save (Signal), left to
 * right; or a single Close when read-only. Sits above the system navigation bar.
 */
@Composable
fun AreaActionBar(
    editable: Boolean,
    canErase: Boolean,
    canSave: Boolean,
    onCancel: () -> Unit,
    onEraseAll: () -> Unit,
    onSave: () -> Unit,
    modifier: Modifier = Modifier,
) {
    Column(modifier.fillMaxWidth().background(ThePreyTheme.colors.void.copy(alpha = CHROME_ALPHA))) {
        HorizontalDivider(thickness = 1.dp, color = MaterialTheme.colorScheme.outlineVariant)
        Row(
            Modifier
                .navigationBarsPadding()
                .fillMaxWidth()
                .padding(horizontal = 12.dp, vertical = 12.dp),
            horizontalArrangement = Arrangement.spacedBy(8.dp),
        ) {
            if (editable) {
                BarButton(stringResource(R.string.playfield_area_cancel), onCancel, BarButtonKind.Neutral, Modifier.weight(1f))
                BarButton(
                    stringResource(R.string.playfield_area_erase_all),
                    onEraseAll,
                    BarButtonKind.Danger,
                    Modifier.weight(1f),
                    enabled = canErase,
                )
                BarButton(
                    stringResource(R.string.playfield_area_save),
                    onSave,
                    BarButtonKind.Confirm,
                    Modifier.weight(1f),
                    enabled = canSave,
                )
            } else {
                BarButton(stringResource(R.string.playfield_area_close), onCancel, BarButtonKind.Neutral, Modifier.weight(1f))
            }
        }
    }
}

private enum class BarButtonKind { Neutral, Danger, Confirm }

private val barLabelStyle = TextStyle(fontFamily = BodyFontFamily, fontSize = 13.sp, letterSpacing = 0.6.sp)

/** A compact bar action: three fit side by side, also with the longer Dutch labels. */
@Composable
private fun BarButton(
    text: String,
    onClick: () -> Unit,
    kind: BarButtonKind,
    modifier: Modifier = Modifier,
    enabled: Boolean = true,
    height: Dp = BarButtonHeight,
) {
    val padding = PaddingValues(horizontal = 6.dp)
    val label: @Composable () -> Unit = {
        Text(text = text.uppercase(), style = barLabelStyle, maxLines = 1, overflow = TextOverflow.Ellipsis)
    }
    when (kind) {
        BarButtonKind.Neutral -> OutlinedButton(
            onClick = onClick,
            enabled = enabled,
            modifier = modifier.height(height),
            shape = MaterialTheme.shapes.small,
            contentPadding = padding,
            border = BorderStroke(1.dp, MaterialTheme.colorScheme.outline),
            colors = ButtonDefaults.outlinedButtonColors(
                contentColor = MaterialTheme.colorScheme.onSurface,
                disabledContentColor = ThePreyTheme.colors.textGhost,
            ),
        ) { label() }
        BarButtonKind.Danger, BarButtonKind.Confirm -> Button(
            onClick = onClick,
            enabled = enabled,
            modifier = modifier.height(height),
            shape = MaterialTheme.shapes.small,
            contentPadding = padding,
            colors = ButtonDefaults.buttonColors(
                containerColor = if (kind == BarButtonKind.Danger) ThePreyTheme.colors.hunter else MaterialTheme.colorScheme.primary,
                contentColor = if (kind == BarButtonKind.Danger) MaterialTheme.colorScheme.onError else MaterialTheme.colorScheme.onPrimary,
                disabledContainerColor = MaterialTheme.colorScheme.surfaceVariant,
                disabledContentColor = MaterialTheme.colorScheme.onSurfaceVariant,
            ),
        ) { label() }
    }
}

// ---- Previews (the map itself needs a device; these show the chrome) ----

@Composable
private fun BarPreview(state: PlayFieldAreaUiState) {
    ThePreyTheme {
        Box(Modifier.background(MaterialTheme.colorScheme.background).padding(top = 24.dp)) {
            Column {
                MapAttribution(Modifier.align(Alignment.End).padding(end = 8.dp, bottom = 4.dp))
                AreaActionBar(
                    editable = state.editable,
                    canErase = state.canErase,
                    canSave = state.canSave,
                    onCancel = {},
                    onEraseAll = {},
                    onSave = {},
                )
            }
        }
    }
}

private val previewArea = listOf(
    GeoPosition(52.36, 4.86),
    GeoPosition(52.36, 4.88),
    GeoPosition(52.35, 4.88),
    GeoPosition(52.35, 4.86),
)

@Preview(name = "Area bar, no points")
@Composable
private fun EmptyBarPreview() = BarPreview(PlayFieldAreaUiState(points = emptyList(), editable = true))

@Preview(name = "Area bar, no points (Dutch)", locale = "nl")
@Composable
private fun EmptyBarDutchPreview() = BarPreview(PlayFieldAreaUiState(points = emptyList(), editable = true))

@Preview(name = "Area bar, 4 points")
@Composable
private fun AreaBarPreview() = BarPreview(PlayFieldAreaUiState(points = previewArea, editable = true))

@Preview(name = "Area bar, 4 points (Dutch)", locale = "nl")
@Composable
private fun AreaBarDutchPreview() = BarPreview(PlayFieldAreaUiState(points = previewArea, editable = true))

@Preview(name = "Area bar, read-only")
@Composable
private fun ReadOnlyBarPreview() = BarPreview(PlayFieldAreaUiState(points = previewArea, editable = false))

@Preview(name = "Area bar, read-only (Dutch)", locale = "nl")
@Composable
private fun ReadOnlyBarDutchPreview() = BarPreview(PlayFieldAreaUiState(points = previewArea, editable = false))
