package nl.hexmaster.theprey.ui.playfields.details

import androidx.activity.compose.BackHandler
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.navigationBarsPadding
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.layout.widthIn
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.selection.toggleable
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.Checkbox
import androidx.compose.material3.CheckboxDefaults
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.OutlinedTextFieldDefaults
import androidx.compose.material3.Scaffold
import androidx.compose.material3.SnackbarHost
import androidx.compose.material3.SnackbarHostState
import androidx.compose.material3.Text
import androidx.compose.material3.TopAppBar
import androidx.compose.material3.TopAppBarDefaults
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberUpdatedState
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.res.pluralStringResource
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.semantics.Role
import androidx.compose.ui.text.input.ImeAction
import androidx.compose.ui.text.input.KeyboardCapitalization
import androidx.compose.ui.tooling.preview.Preview
import androidx.compose.ui.unit.dp
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import nl.hexmaster.theprey.R
import nl.hexmaster.theprey.data.location.GeoPosition
import nl.hexmaster.theprey.ui.components.PrimaryButton
import nl.hexmaster.theprey.ui.components.SecondaryButton
import nl.hexmaster.theprey.ui.playfields.MessageState
import nl.hexmaster.theprey.ui.playfields.RowSeparator
import nl.hexmaster.theprey.ui.theme.ThePreyTheme

/**
 * One playfield: its name, the Public checkbox (only for public-eligible names) and the entry to
 * its area. Editable when opened from the Private tab: Cancel and Save (one update, then back to
 * the list). Read-only when opened from the Public tab: Close only. Cancel, Close, the back arrow
 * and system Back leave without saving; nothing is sent until Save.
 */
@Composable
fun PlayFieldDetailsScreen(
    viewModel: PlayFieldDetailsViewModel,
    onClose: () -> Unit,
    onOpenArea: () -> Unit,
    modifier: Modifier = Modifier,
) {
    val uiState by viewModel.uiState.collectAsStateWithLifecycle()
    val snackbarHostState = remember { SnackbarHostState() }
    val saveFailedText = stringResource(R.string.playfield_details_save_failed)
    val conflictText = stringResource(R.string.playfield_details_conflict)
    val currentOnClose by rememberUpdatedState(onClose)

    LaunchedEffect(viewModel) {
        viewModel.detailsEvents.collect { event ->
            when (event) {
                DetailsEvent.Closed -> currentOnClose()
                DetailsEvent.SaveFailed -> snackbarHostState.showSnackbar(saveFailedText)
                DetailsEvent.Conflict -> snackbarHostState.showSnackbar(conflictText)
            }
        }
    }
    // Back is Cancel; while a save runs nothing can be left or changed.
    BackHandler {
        if (!uiState.saving) onClose()
    }

    PlayFieldDetailsContent(
        uiState = uiState,
        snackbarHostState = snackbarHostState,
        onClose = onClose,
        onRetry = viewModel::retry,
        onNameChange = viewModel::onNameChange,
        onPublicChange = viewModel::onPublicChange,
        onOpenArea = onOpenArea,
        onSave = viewModel::save,
        modifier = modifier,
    )
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun PlayFieldDetailsContent(
    uiState: PlayFieldDetailsUiState,
    snackbarHostState: SnackbarHostState,
    onClose: () -> Unit,
    onRetry: () -> Unit,
    onNameChange: (String) -> Unit,
    onPublicChange: (Boolean) -> Unit,
    onOpenArea: () -> Unit,
    onSave: () -> Unit,
    modifier: Modifier = Modifier,
) {
    Scaffold(
        modifier = modifier.fillMaxSize(),
        containerColor = MaterialTheme.colorScheme.background,
        topBar = {
            TopAppBar(
                title = {
                    Text(text = stringResource(R.string.playfield_details_title), style = MaterialTheme.typography.titleLarge)
                },
                navigationIcon = {
                    IconButton(onClick = onClose, enabled = !uiState.saving) {
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
        bottomBar = {
            if (uiState.load == DetailsLoad.Loaded) {
                DetailsActionBar(uiState = uiState, onClose = onClose, onSave = onSave)
            }
        },
    ) { innerPadding ->
        val contentModifier = Modifier.padding(innerPadding)
        when (uiState.load) {
            DetailsLoad.Loading -> MessageState(text = null, loading = true, modifier = contentModifier)
            DetailsLoad.NotFound -> MessageState(
                text = stringResource(R.string.playfield_details_not_found),
                actionLabel = stringResource(R.string.playfield_details_close),
                onAction = onClose,
                modifier = contentModifier,
            )
            DetailsLoad.Error -> MessageState(
                text = stringResource(R.string.playfield_details_load_error),
                actionLabel = stringResource(R.string.playfield_details_retry),
                onAction = onRetry,
                modifier = contentModifier,
            )
            DetailsLoad.Loaded -> DetailsForm(
                uiState = uiState,
                onNameChange = onNameChange,
                onPublicChange = onPublicChange,
                onOpenArea = onOpenArea,
                modifier = contentModifier,
            )
        }
    }
}

@Composable
private fun DetailsForm(
    uiState: PlayFieldDetailsUiState,
    onNameChange: (String) -> Unit,
    onPublicChange: (Boolean) -> Unit,
    onOpenArea: () -> Unit,
    modifier: Modifier = Modifier,
) {
    val publicEnabled = uiState.publicEnabled && !uiState.saving
    Column(
        modifier
            .fillMaxSize()
            .verticalScroll(rememberScrollState())
            .padding(horizontal = 18.dp, vertical = 12.dp),
        horizontalAlignment = Alignment.CenterHorizontally,
    ) {
        Column(Modifier.widthIn(max = 560.dp).fillMaxWidth()) {
            if (uiState.editable) {
                OutlinedTextField(
                    value = uiState.name,
                    onValueChange = onNameChange,
                    enabled = !uiState.saving,
                    label = { Text(stringResource(R.string.playfield_details_name_label)) },
                    singleLine = true,
                    keyboardOptions = KeyboardOptions(capitalization = KeyboardCapitalization.Words, imeAction = ImeAction.Done),
                    shape = MaterialTheme.shapes.small,
                    colors = OutlinedTextFieldDefaults.colors(
                        focusedBorderColor = MaterialTheme.colorScheme.primary,
                        unfocusedBorderColor = MaterialTheme.colorScheme.outline,
                        focusedLabelColor = MaterialTheme.colorScheme.primary,
                        unfocusedLabelColor = MaterialTheme.colorScheme.onSurfaceVariant,
                        cursorColor = MaterialTheme.colorScheme.primary,
                    ),
                    modifier = Modifier.fillMaxWidth(),
                )
            } else {
                Text(
                    text = uiState.name,
                    style = MaterialTheme.typography.headlineSmall,
                    color = MaterialTheme.colorScheme.onBackground,
                )
            }

            Spacer(Modifier.height(16.dp))
            Row(
                Modifier
                    .fillMaxWidth()
                    .toggleable(
                        value = uiState.isPublic,
                        enabled = publicEnabled,
                        role = Role.Checkbox,
                        onValueChange = onPublicChange,
                    )
                    .padding(vertical = 4.dp),
                verticalAlignment = Alignment.CenterVertically,
            ) {
                Checkbox(
                    checked = uiState.isPublic,
                    onCheckedChange = null,
                    enabled = publicEnabled,
                    colors = CheckboxDefaults.colors(
                        checkedColor = MaterialTheme.colorScheme.primary,
                        uncheckedColor = MaterialTheme.colorScheme.outline,
                        checkmarkColor = MaterialTheme.colorScheme.onPrimary,
                        disabledCheckedColor = ThePreyTheme.colors.textGhost,
                        disabledUncheckedColor = ThePreyTheme.colors.textGhost,
                    ),
                )
                Spacer(Modifier.width(12.dp))
                Text(
                    text = stringResource(R.string.playfield_details_public),
                    style = MaterialTheme.typography.bodyLarge,
                    color = if (publicEnabled) MaterialTheme.colorScheme.onBackground else MaterialTheme.colorScheme.onSurfaceVariant,
                )
            }
            if (uiState.editable) {
                Text(
                    text = stringResource(R.string.playfield_details_public_hint),
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                    modifier = Modifier.padding(start = 4.dp, top = 4.dp),
                )
            }

            Spacer(Modifier.height(20.dp))
            RowSeparator()
            Spacer(Modifier.height(16.dp))
            Text(
                text = pluralStringResource(R.plurals.playfield_details_points, uiState.points.size, uiState.points.size),
                style = MaterialTheme.typography.bodyMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
            Spacer(Modifier.height(12.dp))
            SecondaryButton(
                text = stringResource(R.string.playfield_details_area),
                onClick = onOpenArea,
                enabled = !uiState.saving,
            )
        }
    }
}

/** Cancel and Save (busy while saving), or Close when read-only. */
@Composable
private fun DetailsActionBar(uiState: PlayFieldDetailsUiState, onClose: () -> Unit, onSave: () -> Unit) {
    Column(Modifier.fillMaxWidth().background(MaterialTheme.colorScheme.background)) {
        RowSeparator()
        Row(
            Modifier
                .navigationBarsPadding()
                .fillMaxWidth()
                .padding(horizontal = 18.dp, vertical = 12.dp),
            horizontalArrangement = Arrangement.spacedBy(12.dp),
        ) {
            if (uiState.editable) {
                SecondaryButton(
                    text = stringResource(R.string.playfield_details_cancel),
                    onClick = onClose,
                    enabled = !uiState.saving,
                    modifier = Modifier.weight(1f),
                )
                PrimaryButton(
                    text = stringResource(R.string.playfield_details_save),
                    onClick = onSave,
                    busy = uiState.saving,
                    busyText = stringResource(R.string.playfield_details_saving),
                    enabled = uiState.canSave,
                    modifier = Modifier.weight(1f),
                )
            } else {
                SecondaryButton(text = stringResource(R.string.playfield_details_close), onClick = onClose)
            }
        }
    }
}

// ---- Previews ----

private val previewPoints = listOf(
    GeoPosition(52.36, 4.86),
    GeoPosition(52.36, 4.88),
    GeoPosition(52.35, 4.88),
    GeoPosition(52.35, 4.86),
)

private fun previewState(
    editable: Boolean = true,
    load: DetailsLoad = DetailsLoad.Loaded,
    name: String = "NL, Amsterdam, Vondelpark",
    isPublic: Boolean = true,
    saving: Boolean = false,
) = PlayFieldDetailsUiState(
    editable = editable,
    load = load,
    name = name,
    isPublic = isPublic,
    points = previewPoints,
    saving = saving,
    original = PlayFieldDraft("Vondelpark", isPublic = false, points = previewPoints),
)

@Composable
private fun ContentPreview(uiState: PlayFieldDetailsUiState) {
    ThePreyTheme {
        PlayFieldDetailsContent(
            uiState = uiState,
            snackbarHostState = remember { SnackbarHostState() },
            onClose = {},
            onRetry = {},
            onNameChange = {},
            onPublicChange = {},
            onOpenArea = {},
            onSave = {},
        )
    }
}

@Preview(name = "Loading")
@Composable
private fun LoadingPreview() = ContentPreview(previewState(load = DetailsLoad.Loading))

@Preview(name = "Editable, eligible name")
@Composable
private fun EligiblePreview() = ContentPreview(previewState())

@Preview(name = "Editable, eligible name (Dutch)", locale = "nl")
@Composable
private fun EligibleDutchPreview() = ContentPreview(previewState())

@Preview(name = "Editable, ineligible name")
@Composable
private fun IneligiblePreview() = ContentPreview(previewState(name = "Vondelpark", isPublic = false))

@Preview(name = "Editable, ineligible name (Dutch)", locale = "nl")
@Composable
private fun IneligibleDutchPreview() = ContentPreview(previewState(name = "Vondelpark", isPublic = false))

@Preview(name = "Saving")
@Composable
private fun SavingPreview() = ContentPreview(previewState(saving = true))

@Preview(name = "Read-only")
@Composable
private fun ReadOnlyPreview() = ContentPreview(previewState(editable = false))

@Preview(name = "Read-only (Dutch)", locale = "nl")
@Composable
private fun ReadOnlyDutchPreview() = ContentPreview(previewState(editable = false))

@Preview(name = "Not found")
@Composable
private fun NotFoundPreview() = ContentPreview(previewState(load = DetailsLoad.NotFound))

@Preview(name = "Not found (Dutch)", locale = "nl")
@Composable
private fun NotFoundDutchPreview() = ContentPreview(previewState(load = DetailsLoad.NotFound))

@Preview(name = "Error")
@Composable
private fun ErrorPreview() = ContentPreview(previewState(load = DetailsLoad.Error))

@Preview(name = "Error (Dutch)", locale = "nl")
@Composable
private fun ErrorDutchPreview() = ContentPreview(previewState(load = DetailsLoad.Error))
