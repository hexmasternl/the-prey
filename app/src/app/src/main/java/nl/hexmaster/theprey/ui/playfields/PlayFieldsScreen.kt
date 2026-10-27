package nl.hexmaster.theprey.ui.playfields

import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.gestures.detectTapGestures
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.lazy.rememberLazyListState
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.FloatingActionButton
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Scaffold
import androidx.compose.material3.SnackbarHost
import androidx.compose.material3.SnackbarHostState
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.material3.TopAppBar
import androidx.compose.material3.TopAppBarDefaults
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.runtime.snapshotFlow
import androidx.compose.ui.Modifier
import androidx.compose.ui.input.pointer.pointerInput
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.tooling.preview.Preview
import androidx.compose.ui.unit.dp
import androidx.lifecycle.compose.LifecycleResumeEffect
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import kotlinx.coroutines.flow.filter
import nl.hexmaster.theprey.R
import nl.hexmaster.theprey.data.location.GeoPosition
import nl.hexmaster.theprey.ui.theme.ThePreyTheme

/**
 * The playfields list: the player's own playfields (Private tab, with swipe-to-delete and the
 * create button) and the public search (Public tab, read-only rows). Tapping a row opens its
 * details ([onOpen]). The own list is downloaded again every time the screen resumes, e.g. when
 * the player comes back from the details or the editor.
 */
@Composable
fun PlayFieldsScreen(
    viewModel: PlayFieldsViewModel,
    onBack: () -> Unit,
    onCreate: () -> Unit,
    /** Opens a playfield: editable from the Private tab, read-only from the Public tab. */
    onOpen: (id: String, editable: Boolean) -> Unit,
    modifier: Modifier = Modifier,
) {
    val uiState by viewModel.uiState.collectAsStateWithLifecycle()
    val snackbarHostState = remember { SnackbarHostState() }
    val deleteFailedText = stringResource(R.string.playfields_delete_failed)

    LifecycleResumeEffect(viewModel) {
        viewModel.refreshMine()
        onPauseOrDispose { }
    }
    LaunchedEffect(viewModel) {
        viewModel.deleteFailed.collect { snackbarHostState.showSnackbar(deleteFailedText) }
    }

    PlayFieldsContent(
        uiState = uiState,
        snackbarHostState = snackbarHostState,
        onBack = onBack,
        onCreate = onCreate,
        onOpen = onOpen,
        onSelectTab = viewModel::selectTab,
        onRetry = viewModel::refreshMine,
        onRequestDelete = viewModel::requestDelete,
        onCancelDelete = viewModel::cancelDelete,
        onConfirmDelete = viewModel::confirmDelete,
        onQueryChange = viewModel::onQueryChange,
        onToggleNearMe = viewModel::toggleNearMe,
        modifier = modifier,
    )
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun PlayFieldsContent(
    uiState: PlayFieldsUiState,
    snackbarHostState: SnackbarHostState,
    onBack: () -> Unit,
    onCreate: () -> Unit,
    /** Opens a playfield: editable from the Private tab, read-only from the Public tab. */
    onOpen: (id: String, editable: Boolean) -> Unit,
    onSelectTab: (PlayFieldsTab) -> Unit,
    onRetry: () -> Unit,
    onRequestDelete: (PlayFieldItem) -> Unit,
    onCancelDelete: () -> Unit,
    onConfirmDelete: () -> Unit,
    onQueryChange: (String) -> Unit,
    onToggleNearMe: () -> Unit,
    modifier: Modifier = Modifier,
) {
    // At most one Private row shows its Delete button; the view model does not need to know.
    var revealedId by rememberSaveable { mutableStateOf<String?>(null) }

    Scaffold(
        modifier = modifier.fillMaxSize(),
        containerColor = MaterialTheme.colorScheme.background,
        topBar = {
            TopAppBar(
                title = {
                    Text(
                        text = stringResource(R.string.playfields_title),
                        style = MaterialTheme.typography.titleLarge,
                    )
                },
                navigationIcon = {
                    IconButton(onClick = onBack) {
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
        snackbarHost = { SnackbarHost(snackbarHostState) },
        floatingActionButton = {
            if (uiState.selectedTab == PlayFieldsTab.Private) {
                FloatingActionButton(
                    onClick = onCreate,
                    containerColor = MaterialTheme.colorScheme.primary,
                    contentColor = MaterialTheme.colorScheme.onPrimary,
                ) {
                    Icon(
                        painter = painterResource(R.drawable.ic_add),
                        contentDescription = stringResource(R.string.playfields_create),
                    )
                }
            }
        },
    ) { innerPadding ->
        Column(Modifier.padding(innerPadding).fillMaxSize()) {
            PlayFieldTabRow(
                selectedTab = uiState.selectedTab,
                onSelectTab = { tab ->
                    revealedId = null
                    onSelectTab(tab)
                },
            )
            when (uiState.selectedTab) {
                PlayFieldsTab.Private -> PrivateTab(
                    mine = uiState.mine,
                    deletingIds = uiState.deletingIds,
                    revealedId = revealedId,
                    onRevealedChange = { revealedId = it },
                    onRetry = onRetry,
                    onRequestDelete = onRequestDelete,
                    onOpen = { onOpen(it, true) },
                )
                PlayFieldsTab.Public -> PublicTab(
                    query = uiState.query,
                    search = uiState.search,
                    nearMe = uiState.nearMe,
                    onQueryChange = onQueryChange,
                    onToggleNearMe = onToggleNearMe,
                    onOpen = { onOpen(it, false) },
                )
            }
        }
    }

    uiState.pendingDelete?.let { item ->
        DeleteConfirmationDialog(
            name = item.name,
            onConfirm = {
                revealedId = null
                onConfirmDelete()
            },
            onDismiss = {
                revealedId = null
                onCancelDelete()
            },
        )
    }
}

@Composable
private fun PrivateTab(
    mine: ListState,
    deletingIds: Set<String>,
    revealedId: String?,
    onRevealedChange: (String?) -> Unit,
    onRetry: () -> Unit,
    onRequestDelete: (PlayFieldItem) -> Unit,
    onOpen: (id: String) -> Unit,
) {
    OwnPlayFieldsState(mine = mine, emptyText = stringResource(R.string.playfields_empty), onRetry = onRetry) { items ->
        val listState = rememberLazyListState()
        LaunchedEffect(listState) {
            snapshotFlow { listState.isScrollInProgress }.filter { it }.collect { onRevealedChange(null) }
        }
        val deleteLabel = stringResource(R.string.playfields_delete)
        LazyColumn(
            state = listState,
            // Room below the last row so the create button never covers it.
            contentPadding = PaddingValues(bottom = 88.dp),
            modifier = Modifier
                .fillMaxSize()
                // Taps that no row consumes (between or below rows) close the revealed row.
                .pointerInput(Unit) { detectTapGestures { onRevealedChange(null) } },
        ) {
            items(items, key = { it.id }) { item ->
                val busy = item.id in deletingIds
                SwipeRevealRow(
                    revealed = revealedId == item.id,
                    onRevealChange = { open ->
                        when {
                            open -> onRevealedChange(item.id)
                            revealedId == item.id -> onRevealedChange(null)
                        }
                    },
                    enabled = !busy,
                    actionLabel = deleteLabel,
                    onAction = { onRequestDelete(item) },
                ) {
                    PlayFieldRow(
                        item,
                        busy = busy,
                        // With a Delete button revealed anywhere, a tap only closes it.
                        modifier = Modifier.clickable {
                            if (revealedId != null) onRevealedChange(null) else onOpen(item.id)
                        },
                    )
                }
                RowSeparator()
            }
        }
    }
}

@Composable
private fun PublicTab(
    query: String,
    search: SearchState,
    nearMe: NearMeState,
    onQueryChange: (String) -> Unit,
    onToggleNearMe: () -> Unit,
    onOpen: (id: String) -> Unit,
) {
    Column(Modifier.fillMaxSize()) {
        PlayFieldSearchField(query = query, onQueryChange = onQueryChange)
        NearMeFilter(nearMe, onToggleNearMe)
        PublicSearchState(search, nearMe) { items ->
            LazyColumn(Modifier.fillMaxSize()) {
                items(items, key = { it.id }) { item ->
                    // Public playfields can never be removed here, not even the player's own.
                    SwipeRevealRow(
                        revealed = false,
                        onRevealChange = {},
                        enabled = false,
                        actionLabel = "",
                        onAction = {},
                    ) {
                        PlayFieldRow(item, modifier = Modifier.clickable { onOpen(item.id) })
                    }
                    RowSeparator()
                }
            }
        }
    }
}

@Composable
private fun DeleteConfirmationDialog(name: String, onConfirm: () -> Unit, onDismiss: () -> Unit) {
    AlertDialog(
        onDismissRequest = onDismiss,
        containerColor = MaterialTheme.colorScheme.surface,
        titleContentColor = MaterialTheme.colorScheme.onSurface,
        textContentColor = MaterialTheme.colorScheme.onSurfaceVariant,
        title = {
            Text(stringResource(R.string.playfields_delete_confirm_title), style = MaterialTheme.typography.headlineSmall)
        },
        text = {
            Text(stringResource(R.string.playfields_delete_confirm_body, name), style = MaterialTheme.typography.bodyMedium)
        },
        confirmButton = {
            TextButton(onClick = onConfirm) {
                Text(
                    text = stringResource(R.string.playfields_delete).uppercase(),
                    style = MaterialTheme.typography.labelLarge,
                    color = ThePreyTheme.colors.hunter,
                )
            }
        },
        dismissButton = {
            TextButton(onClick = onDismiss) {
                Text(
                    text = stringResource(R.string.playfields_cancel).uppercase(),
                    style = MaterialTheme.typography.labelLarge,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )
            }
        },
    )
}

// ---- Previews ----

@Composable
private fun ContentPreview(uiState: PlayFieldsUiState) {
    ThePreyTheme {
        PlayFieldsContent(
            uiState = uiState,
            snackbarHostState = remember { SnackbarHostState() },
            onBack = {},
            onCreate = {},
            onOpen = { _, _ -> },
            onSelectTab = {},
            onRetry = {},
            onRequestDelete = {},
            onCancelDelete = {},
            onConfirmDelete = {},
            onQueryChange = {},
            onToggleNearMe = {},
            modifier = Modifier.background(MaterialTheme.colorScheme.background),
        )
    }
}

@Preview(name = "Loading")
@Composable
private fun LoadingPreview() = ContentPreview(PlayFieldsUiState(mine = ListState.Loading))

@Preview(name = "List")
@Composable
private fun ListPreview() =
    ContentPreview(PlayFieldsUiState(mine = ListState.Loaded(previewItems), deletingIds = setOf("2")))

@Preview(name = "List (Dutch)", locale = "nl")
@Composable
private fun ListDutchPreview() = ContentPreview(PlayFieldsUiState(mine = ListState.Loaded(previewItems)))

@Preview(name = "Empty")
@Composable
private fun EmptyPreview() = ContentPreview(PlayFieldsUiState(mine = ListState.Loaded(emptyList())))

@Preview(name = "Error")
@Composable
private fun ErrorPreview() = ContentPreview(PlayFieldsUiState(mine = ListState.Error))

@Preview(name = "Confirm delete")
@Composable
private fun ConfirmDeletePreview() = ContentPreview(
    PlayFieldsUiState(mine = ListState.Loaded(previewItems), pendingDelete = previewItems[0]),
)

@Preview(name = "Public results")
@Composable
private fun PublicResultsPreview() = ContentPreview(
    PlayFieldsUiState(
        selectedTab = PlayFieldsTab.Public,
        query = "park",
        search = SearchState.Results(previewItems.take(1)),
    ),
)

@Preview(name = "Near me results")
@Composable
private fun NearMeResultsPreview() = ContentPreview(
    PlayFieldsUiState(
        selectedTab = PlayFieldsTab.Public,
        nearMe = NearMeState.On(GeoPosition(52.36, 4.87)),
        search = SearchState.Results(previewItems.mapIndexed { i, item -> item.copy(distanceMeters = 850 + i * 1_400) }),
    ),
)

@Preview(name = "Near me results (Dutch)", locale = "nl")
@Composable
private fun NearMeResultsDutchPreview() = NearMeResultsPreview()

@Preview(name = "Near me without permission")
@Composable
private fun NearMeNoPermissionPreview() = ContentPreview(
    PlayFieldsUiState(selectedTab = PlayFieldsTab.Public, nearMe = NearMeState.NoPermission),
)

@Preview(name = "Near me: nothing nearby (Dutch)", locale = "nl")
@Composable
private fun NearMeNoneDutchPreview() = ContentPreview(
    PlayFieldsUiState(
        selectedTab = PlayFieldsTab.Public,
        nearMe = NearMeState.On(GeoPosition(52.36, 4.87)),
        search = SearchState.Results(emptyList()),
    ),
)

@Preview(name = "Public hint (Dutch)", locale = "nl")
@Composable
private fun PublicHintDutchPreview() =
    ContentPreview(PlayFieldsUiState(selectedTab = PlayFieldsTab.Public, query = "vo"))
