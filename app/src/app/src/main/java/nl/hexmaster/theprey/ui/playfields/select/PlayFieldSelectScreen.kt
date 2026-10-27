package nl.hexmaster.theprey.ui.playfields.select

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.imePadding
import androidx.compose.foundation.layout.navigationBarsPadding
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.selection.selectable
import androidx.compose.foundation.selection.selectableGroup
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Text
import androidx.compose.material3.TopAppBar
import androidx.compose.material3.TopAppBarDefaults
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.semantics.Role
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.tooling.preview.Preview
import androidx.compose.ui.unit.dp
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import nl.hexmaster.theprey.R
import nl.hexmaster.theprey.ui.components.PrimaryButton
import nl.hexmaster.theprey.ui.components.SecondaryButton
import nl.hexmaster.theprey.ui.playfields.ListState
import nl.hexmaster.theprey.ui.playfields.OwnPlayFieldsState
import nl.hexmaster.theprey.ui.playfields.PlayFieldItem
import nl.hexmaster.theprey.ui.playfields.PlayFieldRow
import nl.hexmaster.theprey.ui.playfields.NearMeFilter
import nl.hexmaster.theprey.ui.playfields.PlayFieldSearchField
import nl.hexmaster.theprey.ui.playfields.PlayFieldTabRow
import nl.hexmaster.theprey.ui.playfields.PlayFieldsTab
import nl.hexmaster.theprey.ui.playfields.PublicSearchState
import nl.hexmaster.theprey.ui.playfields.RowSeparator
import nl.hexmaster.theprey.ui.playfields.SearchState
import nl.hexmaster.theprey.ui.playfields.previewItems
import nl.hexmaster.theprey.ui.theme.ThePreyTheme

/**
 * Lets the player pick one playfield, from their own (Private) or the public search (Public), and
 * hands it back to the flow that opened the screen. Rows are read-only: a tap only changes the
 * selection. Back needs no handler: popping the entry without a result is already Cancel.
 */
@Composable
fun PlayFieldSelectScreen(
    viewModel: PlayFieldSelectViewModel,
    onCancel: () -> Unit,
    onSelect: (PlayFieldSelection) -> Unit,
    modifier: Modifier = Modifier,
) {
    val uiState by viewModel.uiState.collectAsStateWithLifecycle()
    PlayFieldSelectContent(
        uiState = uiState,
        onCancel = onCancel,
        onSelect = onSelect,
        onSelectTab = viewModel::selectTab,
        onRetry = viewModel::retry,
        onQueryChange = viewModel::onQueryChange,
        onToggleNearMe = viewModel::toggleNearMe,
        onToggle = viewModel::toggle,
        modifier = modifier,
    )
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun PlayFieldSelectContent(
    uiState: PlayFieldSelectUiState,
    onCancel: () -> Unit,
    onSelect: (PlayFieldSelection) -> Unit,
    onSelectTab: (PlayFieldsTab) -> Unit,
    onRetry: () -> Unit,
    onQueryChange: (String) -> Unit,
    onToggleNearMe: () -> Unit = {},
    onToggle: (PlayFieldItem) -> Unit,
    modifier: Modifier = Modifier,
) {
    Scaffold(
        modifier = modifier.fillMaxSize(),
        containerColor = MaterialTheme.colorScheme.background,
        topBar = {
            TopAppBar(
                title = {
                    Text(
                        text = stringResource(R.string.playfield_select_title),
                        style = MaterialTheme.typography.titleLarge,
                    )
                },
                navigationIcon = {
                    IconButton(onClick = onCancel) {
                        Icon(
                            painter = painterResource(R.drawable.ic_arrow_back),
                            contentDescription = stringResource(R.string.navigate_back),
                        )
                    }
                },
                colors = TopAppBarDefaults.topAppBarColors(
                    containerColor = MaterialTheme.colorScheme.background,
                    titleContentColor = MaterialTheme.colorScheme.onBackground,
                    navigationIconContentColor = MaterialTheme.colorScheme.onBackground,
                ),
            )
        },
        bottomBar = { SelectionBar(selected = uiState.selected, onCancel = onCancel, onSelect = onSelect) },
    ) { innerPadding ->
        Column(Modifier.padding(innerPadding).fillMaxSize()) {
            PlayFieldTabRow(selectedTab = uiState.selectedTab, onSelectTab = onSelectTab)
            val selectedId = uiState.selected?.id
            when (uiState.selectedTab) {
                PlayFieldsTab.Private -> OwnPlayFieldsState(
                    mine = uiState.mine,
                    emptyText = stringResource(R.string.playfield_select_empty),
                    onRetry = onRetry,
                ) { items -> SelectableList(items, selectedId, onToggle) }
                PlayFieldsTab.Public -> Column(Modifier.fillMaxSize()) {
                    PlayFieldSearchField(query = uiState.query, onQueryChange = onQueryChange)
                    NearMeFilter(uiState.nearMe, onToggleNearMe)
                    PublicSearchState(uiState.search, uiState.nearMe) { items -> SelectableList(items, selectedId, onToggle) }
                }
            }
        }
    }
}

@Composable
private fun SelectableList(items: List<PlayFieldItem>, selectedId: String?, onToggle: (PlayFieldItem) -> Unit) {
    LazyColumn(Modifier.fillMaxSize().selectableGroup()) {
        items(items, key = { it.id }) { item ->
            val selected = item.id == selectedId
            PlayFieldRow(
                item,
                selected = selected,
                modifier = Modifier.selectable(selected = selected, role = Role.RadioButton, onClick = { onToggle(item) }),
            )
            RowSeparator()
        }
    }
}

/** The selected name with Cancel and Select; stays above the navigation bar and the keyboard. */
@Composable
private fun SelectionBar(
    selected: PlayFieldSelection?,
    onCancel: () -> Unit,
    onSelect: (PlayFieldSelection) -> Unit,
) {
    Column(
        Modifier
            .fillMaxWidth()
            .background(MaterialTheme.colorScheme.surface)
            .navigationBarsPadding()
            .imePadding(),
    ) {
        RowSeparator()
        Column(Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(12.dp)) {
            Text(
                text = selected?.let { stringResource(R.string.playfield_select_selected, it.name) }
                    ?: stringResource(R.string.playfield_select_none),
                style = MaterialTheme.typography.bodyMedium,
                color = if (selected != null) MaterialTheme.colorScheme.onSurface else MaterialTheme.colorScheme.onSurfaceVariant,
                maxLines = 1,
                overflow = TextOverflow.Ellipsis,
            )
            Row(horizontalArrangement = Arrangement.spacedBy(12.dp)) {
                SecondaryButton(
                    text = stringResource(R.string.playfields_cancel),
                    onClick = onCancel,
                    modifier = Modifier.weight(1f),
                    height = 48.dp,
                )
                PrimaryButton(
                    text = stringResource(R.string.playfield_select_confirm),
                    onClick = { selected?.let(onSelect) },
                    enabled = selected != null,
                    modifier = Modifier.weight(1f),
                    height = 48.dp,
                )
            }
        }
    }
}

// ---- Previews ----

@Composable
private fun ContentPreview(uiState: PlayFieldSelectUiState) {
    ThePreyTheme {
        PlayFieldSelectContent(
            uiState = uiState,
            onCancel = {},
            onSelect = {},
            onSelectTab = {},
            onRetry = {},
            onQueryChange = {},
            onToggle = {},
        )
    }
}

private val previewSelection = PlayFieldSelection(previewItems[0].id, previewItems[0].name, previewItems[0].isPublic)

@Preview(name = "Nothing selected")
@Composable
private fun NothingSelectedPreview() = ContentPreview(PlayFieldSelectUiState(mine = ListState.Loaded(previewItems)))

@Preview(name = "Private selected")
@Composable
private fun PrivateSelectedPreview() =
    ContentPreview(PlayFieldSelectUiState(mine = ListState.Loaded(previewItems), selected = previewSelection))

@Preview(name = "Public results selected")
@Composable
private fun PublicSelectedPreview() = ContentPreview(
    PlayFieldSelectUiState(
        selectedTab = PlayFieldsTab.Public,
        query = "park",
        search = SearchState.Results(previewItems.take(1)),
        selected = previewSelection,
    ),
)

@Preview(name = "Empty")
@Composable
private fun EmptyPreview() = ContentPreview(PlayFieldSelectUiState(mine = ListState.Loaded(emptyList())))

@Preview(name = "Error")
@Composable
private fun ErrorPreview() = ContentPreview(PlayFieldSelectUiState(mine = ListState.Error))

@Preview(name = "Selected (Dutch)", locale = "nl")
@Composable
private fun DutchPreview() =
    ContentPreview(PlayFieldSelectUiState(mine = ListState.Loaded(previewItems), selected = previewSelection))
