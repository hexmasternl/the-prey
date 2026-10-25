package nl.hexmaster.theprey.ui.playfields

import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.gestures.AnchoredDraggableDefaults
import androidx.compose.foundation.gestures.AnchoredDraggableState
import androidx.compose.foundation.gestures.DraggableAnchors
import androidx.compose.foundation.gestures.Orientation
import androidx.compose.foundation.gestures.anchoredDraggable
import androidx.compose.foundation.gestures.animateTo
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxHeight
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.offset
import androidx.compose.foundation.layout.width
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberUpdatedState
import androidx.compose.runtime.snapshotFlow
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalDensity
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.semantics.CustomAccessibilityAction
import androidx.compose.ui.semantics.clearAndSetSemantics
import androidx.compose.ui.semantics.customActions
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.tooling.preview.Preview
import androidx.compose.ui.unit.IntOffset
import androidx.compose.ui.unit.dp
import nl.hexmaster.theprey.R
import nl.hexmaster.theprey.ui.theme.ThePreyTheme
import kotlin.math.roundToInt

/** Rest positions of a [SwipeRevealRow]. */
enum class RevealValue { Closed, Revealed }

/** Width of the action button revealed behind a [SwipeRevealRow]. */
private val ActionWidth = 88.dp

/** Release past this share of [ActionWidth] keeps the action revealed; less slides back. */
private const val REVEAL_THRESHOLD = 0.5f

/**
 * A list row that slides left to reveal one danger action (Hunter fill) behind it. The screen owns
 * [revealed], so it can keep at most one row open; [onRevealChange] reports where the player left
 * the row. While revealed, tapping the row closes it. Screen-reader users get the same action as
 * an accessibility action instead of the swipe.
 *
 * With [enabled] = false the row neither slides nor has the button or the accessibility action.
 */
@Composable
fun SwipeRevealRow(
    revealed: Boolean,
    onRevealChange: (Boolean) -> Unit,
    enabled: Boolean,
    actionLabel: String,
    onAction: () -> Unit,
    modifier: Modifier = Modifier,
    content: @Composable () -> Unit,
) {
    if (!enabled) {
        Box(modifier.fillMaxWidth().background(MaterialTheme.colorScheme.surface)) { content() }
        return
    }

    val actionWidthPx = with(LocalDensity.current) { ActionWidth.toPx() }
    val state = remember(actionWidthPx) {
        AnchoredDraggableState(
            initialValue = if (revealed) RevealValue.Revealed else RevealValue.Closed,
            anchors = DraggableAnchors {
                RevealValue.Closed at 0f
                RevealValue.Revealed at -actionWidthPx
            },
        )
    }
    val currentOnRevealChange by rememberUpdatedState(onRevealChange)

    LaunchedEffect(state, revealed) {
        state.animateTo(if (revealed) RevealValue.Revealed else RevealValue.Closed)
    }
    LaunchedEffect(state) {
        snapshotFlow { state.settledValue }.collect { currentOnRevealChange(it == RevealValue.Revealed) }
    }

    val isOpen = state.settledValue == RevealValue.Revealed || state.targetValue == RevealValue.Revealed
    Box(
        modifier
            .fillMaxWidth()
            .semantics {
                customActions = listOf(CustomAccessibilityAction(actionLabel) { onAction(); true })
            },
    ) {
        // The action sits behind the content and shows as the content slides away.
        Box(
            Modifier
                .matchParentSize()
                .then(if (isOpen) Modifier else Modifier.clearAndSetSemantics {}),
            contentAlignment = Alignment.CenterEnd,
        ) {
            Box(
                Modifier
                    .fillMaxHeight()
                    .width(ActionWidth)
                    .background(ThePreyTheme.colors.hunter)
                    .clickable(enabled = isOpen, onClick = onAction),
                contentAlignment = Alignment.Center,
            ) {
                Text(
                    text = actionLabel.uppercase(),
                    style = MaterialTheme.typography.labelMedium,
                    color = MaterialTheme.colorScheme.onError,
                    maxLines = 1,
                )
            }
        }
        Box(
            Modifier
                .fillMaxWidth()
                .offset { IntOffset(state.offset.takeUnless { it.isNaN() }?.roundToInt() ?: 0, 0) }
                .anchoredDraggable(
                    state = state,
                    orientation = Orientation.Horizontal,
                    flingBehavior = AnchoredDraggableDefaults.flingBehavior(
                        state = state,
                        positionalThreshold = { distance -> distance * REVEAL_THRESHOLD },
                    ),
                )
                .background(MaterialTheme.colorScheme.surface)
                .then(if (revealed) Modifier.clickable { onRevealChange(false) } else Modifier),
        ) {
            content()
        }
    }
}

@Preview(name = "Closed and revealed")
@Composable
private fun SwipeRevealRowPreview() {
    ThePreyTheme {
        Column {
            listOf(false, true).forEachIndexed { index, revealed ->
                SwipeRevealRow(
                    revealed = revealed,
                    onRevealChange = {},
                    enabled = true,
                    actionLabel = stringResource(R.string.playfields_delete),
                    onAction = {},
                ) {
                    PlayFieldRow(previewItems[index])
                }
                RowSeparator()
            }
        }
    }
}
