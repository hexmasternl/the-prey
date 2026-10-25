package nl.hexmaster.theprey.ui.game.hud.hunter

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.navigationBarsPadding
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.selection.selectable
import androidx.compose.foundation.selection.selectableGroup
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.ModalBottomSheet
import androidx.compose.material3.RadioButton
import androidx.compose.material3.RadioButtonDefaults
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.material3.rememberModalBottomSheetState
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberUpdatedState
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.semantics.Role
import androidx.compose.ui.tooling.preview.Preview
import androidx.compose.ui.unit.dp
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import nl.hexmaster.theprey.R
import nl.hexmaster.theprey.data.game.TagCandidateDto
import nl.hexmaster.theprey.ui.components.PrimaryButton
import nl.hexmaster.theprey.ui.components.PrimaryButtonStyle
import nl.hexmaster.theprey.ui.game.hud.common.formatDistance
import nl.hexmaster.theprey.ui.playfields.MessageState
import nl.hexmaster.theprey.ui.playfields.RowSeparator
import nl.hexmaster.theprey.ui.theme.ThePreyTheme
import kotlin.math.roundToInt

/**
 * The tag sheet: the preys the server says are in range, nearest first, as single-select rows,
 * and a red confirm. [reload] fetches the list again when the sheet is reopened.
 */
@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun TagSheet(
    viewModel: TagSheetViewModel,
    reload: Boolean,
    onDismiss: () -> Unit,
    onTagged: (callsign: String) -> Unit,
) {
    val state by viewModel.state.collectAsStateWithLifecycle()
    var notice by remember { mutableStateOf<Int?>(null) }
    val currentOnTagged by rememberUpdatedState(onTagged)
    LaunchedEffect(viewModel) {
        if (reload) viewModel.load()
        viewModel.events.collect { event ->
            when (event) {
                is TagSheetEvent.Tagged -> currentOnTagged(event.callsign)
                TagSheetEvent.NoLongerTaggable -> notice = R.string.tag_sheet_no_longer_taggable
            }
        }
    }
    ModalBottomSheet(
        onDismissRequest = onDismiss,
        sheetState = rememberModalBottomSheetState(skipPartiallyExpanded = true),
        containerColor = MaterialTheme.colorScheme.surface,
    ) {
        TagSheetContent(
            state = state,
            notice = notice,
            onSelect = {
                notice = null
                viewModel.select(it)
            },
            onRefresh = {
                notice = null
                viewModel.load()
            },
            onConfirm = viewModel::confirm,
        )
    }
}

@Composable
internal fun TagSheetContent(
    state: TagSheetState,
    notice: Int?,
    onSelect: (String) -> Unit,
    onRefresh: () -> Unit,
    onConfirm: () -> Unit,
) {
    Column(Modifier.fillMaxWidth().navigationBarsPadding().padding(horizontal = 16.dp).padding(bottom = 16.dp)) {
        Row(Modifier.fillMaxWidth(), verticalAlignment = Alignment.CenterVertically) {
            Text(
                text = stringResource(R.string.tag_sheet_title),
                style = MaterialTheme.typography.headlineSmall,
                color = MaterialTheme.colorScheme.onSurface,
                modifier = Modifier.weight(1f),
            )
            TextButton(onClick = onRefresh, enabled = (state as? TagSheetState.Candidates)?.submitting != true) {
                Text(stringResource(R.string.tag_sheet_refresh).uppercase(), style = MaterialTheme.typography.labelLarge)
            }
        }
        notice?.let {
            Text(
                text = stringResource(it),
                style = MaterialTheme.typography.bodySmall,
                color = ThePreyTheme.colors.caution,
                modifier = Modifier.padding(vertical = 8.dp),
            )
        }
        when (state) {
            TagSheetState.Loading -> MessageState(text = null, loading = true, modifier = Modifier.heightIn(max = 200.dp))
            TagSheetState.LoadFailed -> MessageState(
                text = stringResource(R.string.tag_sheet_load_error),
                actionLabel = stringResource(R.string.playfields_retry),
                onAction = onRefresh,
                modifier = Modifier.heightIn(max = 240.dp),
            )
            is TagSheetState.Empty -> MessageState(
                text = stringResource(R.string.tag_sheet_empty, formatDistance(state.rangeMeters.roundToInt())),
                modifier = Modifier.heightIn(max = 200.dp),
            )
            is TagSheetState.Candidates -> Candidates(state, onSelect, onConfirm)
        }
    }
}

@Composable
private fun Candidates(state: TagSheetState.Candidates, onSelect: (String) -> Unit, onConfirm: () -> Unit) {
    Text(
        text = stringResource(R.string.tag_sheet_subtitle, formatDistance(state.rangeMeters.roundToInt())),
        style = MaterialTheme.typography.bodySmall,
        color = MaterialTheme.colorScheme.onSurfaceVariant,
        modifier = Modifier.padding(vertical = 8.dp),
    )
    Column(
        Modifier
            .fillMaxWidth()
            .background(MaterialTheme.colorScheme.surfaceContainerLowest, MaterialTheme.shapes.medium)
            .selectableGroup(),
    ) {
        state.candidates.forEachIndexed { index, candidate ->
            if (index > 0) RowSeparator()
            CandidateRow(candidate, selected = candidate.userId == state.selectedId, enabled = !state.submitting) {
                onSelect(candidate.userId)
            }
        }
    }
    if (state.sendFailed) {
        Text(
            text = stringResource(R.string.tag_sheet_error),
            style = MaterialTheme.typography.bodySmall,
            color = ThePreyTheme.colors.hunter,
            modifier = Modifier.padding(top = 12.dp),
        )
    }
    PrimaryButton(
        text = stringResource(R.string.tag_sheet_confirm),
        busyText = stringResource(R.string.tag_sheet_tagging),
        onClick = onConfirm,
        enabled = state.canConfirm,
        busy = state.submitting,
        style = PrimaryButtonStyle.Hunter,
        modifier = Modifier.padding(top = 16.dp),
    )
}

@Composable
private fun CandidateRow(candidate: TagCandidateDto, selected: Boolean, enabled: Boolean, onClick: () -> Unit) {
    Row(
        Modifier
            .fillMaxWidth()
            .heightIn(min = 48.dp)
            .selectable(selected = selected, enabled = enabled, role = Role.RadioButton, onClick = onClick)
            .padding(horizontal = 12.dp, vertical = 8.dp),
        verticalAlignment = Alignment.CenterVertically,
        horizontalArrangement = Arrangement.spacedBy(12.dp),
    ) {
        RadioButton(
            selected = selected,
            onClick = null,
            enabled = enabled,
            colors = RadioButtonDefaults.colors(selectedColor = ThePreyTheme.colors.hunter),
        )
        Text(
            text = candidate.callsign,
            style = MaterialTheme.typography.titleMedium,
            color = MaterialTheme.colorScheme.onSurface,
            modifier = Modifier.weight(1f),
        )
        Text(
            text = formatDistance(candidate.distanceMeters.roundToInt()),
            style = MaterialTheme.typography.titleMedium,
            color = ThePreyTheme.colors.hunter,
        )
    }
}

// ---- Previews ----

private val previewCandidates = listOf(
    TagCandidateDto("u1", "Vos", "Active", 12.0),
    TagCandidateDto("u2", "Ghost-Runner", "Active", 41.0),
)

@Composable
private fun SheetPreview(state: TagSheetState, notice: Int? = null) {
    ThePreyTheme {
        Column(Modifier.background(MaterialTheme.colorScheme.surface)) {
            TagSheetContent(state = state, notice = notice, onSelect = {}, onRefresh = {}, onConfirm = {})
        }
    }
}

@Preview(name = "Candidates")
@Composable
private fun CandidatesPreview() = SheetPreview(TagSheetState.Candidates(50.0, previewCandidates, selectedId = "u1"))

@Preview(name = "Submitting (Dutch)", locale = "nl")
@Composable
private fun SubmittingPreview() = SheetPreview(TagSheetState.Candidates(50.0, previewCandidates, selectedId = "u1", submitting = true))

@Preview(name = "Send failed")
@Composable
private fun SendFailedPreview() = SheetPreview(TagSheetState.Candidates(50.0, previewCandidates, selectedId = "u2", sendFailed = true))

@Preview(name = "Empty")
@Composable
private fun EmptyPreview() = SheetPreview(TagSheetState.Empty(50.0))

@Preview(name = "Load failed")
@Composable
private fun LoadFailedPreview() = SheetPreview(TagSheetState.LoadFailed)

@Preview(name = "No longer taggable")
@Composable
private fun NoLongerTaggablePreview() =
    SheetPreview(TagSheetState.Candidates(50.0, previewCandidates.drop(1)), notice = R.string.tag_sheet_no_longer_taggable)
