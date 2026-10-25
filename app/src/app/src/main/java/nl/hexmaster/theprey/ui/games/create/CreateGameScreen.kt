package nl.hexmaster.theprey.ui.games.create

import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.navigationBarsPadding
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.material3.TopAppBar
import androidx.compose.material3.TopAppBarDefaults
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.rememberUpdatedState
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.semantics.Role
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.tooling.preview.Preview
import androidx.compose.ui.unit.dp
import androidx.lifecycle.SavedStateHandle
import androidx.lifecycle.compose.LifecycleResumeEffect
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import nl.hexmaster.theprey.R
import nl.hexmaster.theprey.ui.components.PrimaryButton
import nl.hexmaster.theprey.ui.playfields.select.PlayFieldSelectResult
import nl.hexmaster.theprey.ui.playfields.select.PlayFieldSelection
import nl.hexmaster.theprey.ui.theme.ThePreyTheme

/**
 * Create game: choose the playfield (the picker opens by itself first), adjust the five settings
 * and create the game. [resultHandle] is this screen's own back-stack saved state, where the
 * playfield selection screen delivers its result.
 */
@Composable
fun CreateGameScreen(
    viewModel: CreateGameViewModel,
    resultHandle: SavedStateHandle,
    onBack: () -> Unit,
    onChoosePlayField: () -> Unit,
    onCreated: (gameId: String) -> Unit,
    modifier: Modifier = Modifier,
) {
    val uiState by viewModel.uiState.collectAsStateWithLifecycle()
    val currentOnBack by rememberUpdatedState(onBack)
    val currentOnChoose by rememberUpdatedState(onChoosePlayField)
    val currentOnCreated by rememberUpdatedState(onCreated)

    // The result is read synchronously here, so a delivered selection is always applied before
    // deciding whether the first pick was cancelled.
    LifecycleResumeEffect(viewModel, resultHandle) {
        when (viewModel.onResumed(PlayFieldSelectResult.take(resultHandle))) {
            ResumeAction.OpenPicker -> currentOnChoose()
            ResumeAction.Leave -> currentOnBack()
            ResumeAction.None -> Unit
        }
        onPauseOrDispose { }
    }
    LaunchedEffect(viewModel) {
        viewModel.events.collect { event ->
            when (event) {
                is CreateGameEvent.Created -> currentOnCreated(event.gameId)
            }
        }
    }

    CreateGameContent(
        uiState = uiState,
        onBack = onBack,
        onChoosePlayField = onChoosePlayField,
        onSettingChange = viewModel::onSettingChange,
        onCreate = viewModel::create,
        modifier = modifier,
    )
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun CreateGameContent(
    uiState: CreateGameUiState,
    onBack: () -> Unit,
    onChoosePlayField: () -> Unit,
    onSettingChange: (GameSetting, Int) -> Unit,
    onCreate: () -> Unit,
    modifier: Modifier = Modifier,
) {
    Scaffold(
        modifier = modifier.fillMaxSize(),
        containerColor = MaterialTheme.colorScheme.background,
        topBar = {
            TopAppBar(
                title = { Text(stringResource(R.string.game_create_title), style = MaterialTheme.typography.titleLarge) },
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
        bottomBar = { CreateBar(uiState = uiState, onCreate = onCreate) },
    ) { innerPadding ->
        Column(
            Modifier
                .padding(innerPadding)
                .fillMaxSize()
                .verticalScroll(rememberScrollState())
                .padding(16.dp),
            verticalArrangement = Arrangement.spacedBy(32.dp),
        ) {
            PlayFieldCard(
                playField = uiState.playField,
                enabled = !uiState.submitting,
                onClick = onChoosePlayField,
            )
            GameSettingsForm(
                settings = uiState.settings,
                onChange = onSettingChange,
                enabled = !uiState.submitting,
            )
        }
    }
}

/** The chosen playfield (or the prompt to choose one); tapping it opens the picker. */
@Composable
private fun PlayFieldCard(playField: PlayFieldSelection?, enabled: Boolean, onClick: () -> Unit) {
    val changeLabel = stringResource(R.string.game_create_playfield_change)
    Row(
        Modifier
            .fillMaxWidth()
            .heightIn(min = 72.dp)
            .background(MaterialTheme.colorScheme.surface, MaterialTheme.shapes.medium)
            .border(BorderStroke(1.dp, MaterialTheme.colorScheme.outlineVariant), MaterialTheme.shapes.medium)
            .clickable(enabled = enabled, onClickLabel = changeLabel, role = Role.Button, onClick = onClick)
            .padding(16.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Column(Modifier.weight(1f)) {
            Text(
                text = stringResource(R.string.game_create_playfield).uppercase(),
                style = MaterialTheme.typography.labelMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
            Spacer(Modifier.height(6.dp))
            Text(
                text = playField?.name ?: stringResource(R.string.game_create_playfield_choose),
                style = MaterialTheme.typography.titleLarge,
                color = if (playField != null) MaterialTheme.colorScheme.onSurface else MaterialTheme.colorScheme.onSurfaceVariant,
                maxLines = 2,
                overflow = TextOverflow.Ellipsis,
            )
        }
        Icon(
            painter = painterResource(R.drawable.ic_chevron_right),
            contentDescription = null,
            tint = MaterialTheme.colorScheme.primary,
            modifier = Modifier.padding(start = 12.dp),
        )
    }
}

@Composable
private fun CreateBar(uiState: CreateGameUiState, onCreate: () -> Unit) {
    Column(
        Modifier
            .fillMaxWidth()
            .background(MaterialTheme.colorScheme.background)
            .navigationBarsPadding()
            .padding(16.dp),
        verticalArrangement = Arrangement.spacedBy(12.dp),
    ) {
        if (uiState.failed) {
            Row(
                Modifier
                    .fillMaxWidth()
                    .background(ThePreyTheme.colors.hunter.copy(alpha = 0.12f), MaterialTheme.shapes.medium)
                    .border(BorderStroke(1.dp, ThePreyTheme.colors.hunter.copy(alpha = 0.4f)), MaterialTheme.shapes.medium)
                    .padding(start = 16.dp, end = 4.dp, top = 8.dp, bottom = 8.dp),
                verticalAlignment = Alignment.CenterVertically,
            ) {
                Text(
                    text = stringResource(R.string.game_create_failed),
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurface,
                    modifier = Modifier.weight(1f),
                )
                TextButton(onClick = onCreate) {
                    Text(
                        text = stringResource(R.string.playfields_retry).uppercase(),
                        style = MaterialTheme.typography.labelLarge,
                        color = MaterialTheme.colorScheme.primary,
                    )
                }
            }
        }
        PrimaryButton(
            text = stringResource(R.string.game_create_submit),
            busyText = stringResource(R.string.game_create_submitting),
            onClick = onCreate,
            enabled = uiState.canCreate,
            busy = uiState.submitting,
        )
    }
}

// ---- Previews ----

@Composable
private fun ContentPreview(uiState: CreateGameUiState) {
    ThePreyTheme {
        CreateGameContent(uiState = uiState, onBack = {}, onChoosePlayField = {}, onSettingChange = { _, _ -> }, onCreate = {})
    }
}

private val previewPlayField = PlayFieldSelection("1", "Vondelpark", isPublic = true)

@Preview(name = "Empty")
@Composable
private fun EmptyPreview() = ContentPreview(CreateGameUiState())

@Preview(name = "Filled")
@Composable
private fun FilledPreview() = ContentPreview(
    CreateGameUiState(playField = previewPlayField, settings = GameSettings(duration = 60, pingInterval = 2)),
)

@Preview(name = "Submitting")
@Composable
private fun SubmittingPreview() = ContentPreview(CreateGameUiState(playField = previewPlayField, submitting = true))

@Preview(name = "Failed (Dutch)", locale = "nl")
@Composable
private fun FailedPreview() = ContentPreview(CreateGameUiState(playField = previewPlayField, failed = true))
