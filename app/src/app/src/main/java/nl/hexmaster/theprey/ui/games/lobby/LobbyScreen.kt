package nl.hexmaster.theprey.ui.games.lobby

import android.content.Context
import android.content.Intent
import androidx.activity.compose.BackHandler
import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.navigationBarsPadding
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.ExperimentalMaterial3Api
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
import androidx.compose.runtime.rememberUpdatedState
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.platform.LocalResources
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.semantics.Role
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.tooling.preview.Preview
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import nl.hexmaster.theprey.R
import nl.hexmaster.theprey.deeplink.joinLink
import nl.hexmaster.theprey.ui.components.PrimaryButton
import nl.hexmaster.theprey.ui.components.SecondaryButton
import nl.hexmaster.theprey.ui.games.create.GameSetting
import nl.hexmaster.theprey.ui.games.create.GameSettings
import nl.hexmaster.theprey.ui.games.create.GameSettingsForm
import nl.hexmaster.theprey.ui.games.create.GameSettingsSummary
import nl.hexmaster.theprey.ui.playfields.MessageState
import nl.hexmaster.theprey.ui.playfields.RowSeparator
import nl.hexmaster.theprey.ui.theme.ThePreyTheme

/**
 * The game lobby: code and share, the settings (editable for the owner), the players with their
 * role, state and "you" tags, and Set ready or Start game. Back asks before leaving. [onStarted]
 * opens the game when the server reports it started; [onExit] returns to the main menu.
 */
@Composable
fun LobbyScreen(
    viewModel: LobbyViewModel,
    onStarted: (gameId: String) -> Unit,
    onExit: (LobbyExit) -> Unit,
    modifier: Modifier = Modifier,
) {
    val state by viewModel.uiState.collectAsStateWithLifecycle()
    val snackbarHostState = remember { SnackbarHostState() }
    val context = LocalContext.current
    val resources = LocalResources.current
    var confirmLeave by rememberSaveable { mutableStateOf(false) }
    val currentOnStarted by rememberUpdatedState(onStarted)
    val currentOnExit by rememberUpdatedState(onExit)

    LaunchedEffect(viewModel) {
        viewModel.events.collect { event ->
            when (event) {
                is LobbyEvent.Started -> currentOnStarted(event.gameId)
                is LobbyEvent.Exit -> currentOnExit(event.reason)
                is LobbyEvent.Failed -> snackbarHostState.showSnackbar(resources.getString(event.command.errorRes))
            }
        }
    }
    BackHandler { confirmLeave = true }

    LobbyContent(
        state = state,
        snackbarHostState = snackbarHostState,
        onBack = { confirmLeave = true },
        onRetry = viewModel::retry,
        onShare = { code -> context.shareInvite(viewModel.gameId, code) },
        onSettingChange = viewModel::changeSetting,
        onPlayerTap = viewModel::designateHunter,
        onSetReady = viewModel::setReady,
        onStart = viewModel::start,
        modifier = modifier,
    )

    if (confirmLeave) {
        val isOwner = (state as? LobbyScreenState.Content)?.lobby?.isOwner == true
        LeaveDialog(
            isOwner = isOwner,
            onConfirm = {
                confirmLeave = false
                viewModel.leave()
            },
            onDismiss = { confirmLeave = false },
        )
    }
}

private val LobbyCommand.errorRes: Int
    get() = when (this) {
        LobbyCommand.Setting -> R.string.lobby_error_setting
        LobbyCommand.Hunter -> R.string.lobby_error_hunter
        LobbyCommand.Ready -> R.string.lobby_error_ready
        LobbyCommand.Start -> R.string.lobby_error_start
        LobbyCommand.Leave -> R.string.lobby_error_leave
    }

/** Opens the Android share sheet with the localized invitation ending in [code]. */
/** Opens the share sheet with the invitation: its text, the public join link and the code last. */
private fun Context.shareInvite(gameId: String, code: String) {
    val send = Intent(Intent.ACTION_SEND).apply {
        type = "text/plain"
        putExtra(Intent.EXTRA_TEXT, getString(R.string.lobby_share_message, joinLink(gameId, code), code))
    }
    startActivity(Intent.createChooser(send, getString(R.string.lobby_share_title)))
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun LobbyContent(
    state: LobbyScreenState,
    snackbarHostState: SnackbarHostState,
    onBack: () -> Unit,
    onRetry: () -> Unit,
    onShare: (String) -> Unit,
    onSettingChange: (GameSetting, Int) -> Unit,
    onPlayerTap: (String) -> Unit,
    onSetReady: () -> Unit,
    onStart: () -> Unit,
    modifier: Modifier = Modifier,
) {
    val lobby = (state as? LobbyScreenState.Content)?.lobby
    Scaffold(
        modifier = modifier.fillMaxSize(),
        containerColor = MaterialTheme.colorScheme.background,
        topBar = {
            TopAppBar(
                title = { Text(stringResource(R.string.lobby_title), style = MaterialTheme.typography.titleLarge) },
                navigationIcon = {
                    IconButton(onClick = onBack) {
                        Icon(
                            painter = painterResource(R.drawable.ic_arrow_back),
                            contentDescription = stringResource(R.string.navigate_back),
                        )
                    }
                },
                actions = { if (lobby?.reconnecting == true) ReconnectingIndicator(Modifier.padding(end = 16.dp)) },
                colors = TopAppBarDefaults.topAppBarColors(
                    containerColor = MaterialTheme.colorScheme.background,
                    titleContentColor = MaterialTheme.colorScheme.onBackground,
                    navigationIconContentColor = MaterialTheme.colorScheme.onBackground,
                ),
            )
        },
        snackbarHost = { SnackbarHost(snackbarHostState) },
        bottomBar = { if (lobby != null) ActionBar(lobby, onSetReady = onSetReady, onStart = onStart) },
    ) { innerPadding ->
        Box(Modifier.padding(innerPadding).fillMaxSize()) {
            when (state) {
                LobbyScreenState.Loading -> MessageState(text = null, loading = true)
                LobbyScreenState.Error -> MessageState(
                    text = stringResource(R.string.lobby_load_error),
                    actionLabel = stringResource(R.string.playfields_retry),
                    onAction = onRetry,
                )
                is LobbyScreenState.Content -> Column(
                    Modifier
                        .fillMaxSize()
                        .verticalScroll(rememberScrollState())
                        .padding(16.dp),
                    verticalArrangement = Arrangement.spacedBy(32.dp),
                ) {
                    CodeCard(state.lobby.gameCode, onShare)
                    Section(stringResource(R.string.lobby_settings)) {
                        if (state.lobby.isOwner) {
                            GameSettingsForm(settings = state.lobby.settings, onChange = onSettingChange, enabled = state.lobby.canEdit)
                        } else {
                            GameSettingsSummary(settings = state.lobby.settings)
                        }
                    }
                    Section(stringResource(R.string.lobby_players)) {
                        if (state.lobby.isOwner) {
                            Text(
                                text = stringResource(R.string.lobby_players_owner_hint),
                                style = MaterialTheme.typography.bodySmall,
                                color = MaterialTheme.colorScheme.onSurfaceVariant,
                            )
                            Spacer(Modifier.height(8.dp))
                        }
                        PlayerList(state.lobby, onPlayerTap)
                    }
                }
            }
        }
    }
}

@Composable
private fun Section(title: String, content: @Composable () -> Unit) {
    Column {
        Text(
            text = title.uppercase(),
            style = MaterialTheme.typography.labelLarge,
            color = MaterialTheme.colorScheme.secondary,
        )
        Spacer(Modifier.height(12.dp))
        content()
    }
}

@Composable
private fun CodeCard(code: String, onShare: (String) -> Unit) {
    Column(
        Modifier
            .fillMaxWidth()
            .background(MaterialTheme.colorScheme.surface, MaterialTheme.shapes.medium)
            .border(BorderStroke(1.dp, MaterialTheme.colorScheme.outlineVariant), MaterialTheme.shapes.medium)
            .padding(20.dp),
        horizontalAlignment = Alignment.CenterHorizontally,
    ) {
        Text(
            text = stringResource(R.string.lobby_code_label).uppercase(),
            style = MaterialTheme.typography.labelMedium,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )
        Spacer(Modifier.height(8.dp))
        Text(
            text = code,
            style = MaterialTheme.typography.displayMedium.copy(letterSpacing = 12.sp),
            color = MaterialTheme.colorScheme.primary,
            textAlign = TextAlign.Center,
        )
        Spacer(Modifier.height(16.dp))
        SecondaryButton(text = stringResource(R.string.lobby_share), onClick = { onShare(code) }, height = 48.dp)
    }
}

@Composable
private fun PlayerList(lobby: LobbyUiState, onPlayerTap: (String) -> Unit) {
    val makeHunter = stringResource(R.string.lobby_make_hunter)
    Column(
        Modifier
            .fillMaxWidth()
            .background(MaterialTheme.colorScheme.surface, MaterialTheme.shapes.medium)
            .border(BorderStroke(1.dp, MaterialTheme.colorScheme.outlineVariant), MaterialTheme.shapes.medium),
    ) {
        lobby.rows.forEachIndexed { index, row ->
            if (index > 0) RowSeparator()
            Row(
                Modifier
                    .fillMaxWidth()
                    .heightIn(min = 56.dp)
                    .then(
                        if (lobby.isOwner) {
                            Modifier.clickable(
                                enabled = lobby.canEdit,
                                onClickLabel = makeHunter,
                                role = Role.Button,
                                onClick = { onPlayerTap(row.userId) },
                            )
                        } else {
                            Modifier
                        },
                    )
                    .padding(horizontal = 18.dp, vertical = 12.dp),
                verticalAlignment = Alignment.CenterVertically,
                horizontalArrangement = Arrangement.spacedBy(8.dp),
            ) {
                Text(
                    text = row.callsign,
                    style = MaterialTheme.typography.titleMedium,
                    color = MaterialTheme.colorScheme.onSurface,
                    maxLines = 1,
                    overflow = TextOverflow.Ellipsis,
                    modifier = Modifier.weight(1f),
                )
                if (row.isMe) LobbyTag(stringResource(R.string.lobby_tag_you), ThePreyTheme.colors.textGhost, tinted = false)
                if (row.isHunter) {
                    LobbyTag(stringResource(R.string.lobby_tag_hunter), ThePreyTheme.colors.hunter)
                } else {
                    LobbyTag(stringResource(R.string.lobby_tag_prey), ThePreyTheme.colors.prey)
                }
                if (row.isReady) {
                    LobbyTag(stringResource(R.string.lobby_tag_ready), ThePreyTheme.colors.prey)
                } else {
                    LobbyTag(stringResource(R.string.lobby_tag_waiting), ThePreyTheme.colors.caution)
                }
            }
        }
    }
}

/** A status pill: leading dot, tinted fill and border in [color]; [tinted] false is the dim outline variant. */
@Composable
private fun LobbyTag(text: String, color: Color, tinted: Boolean = true) {
    Row(
        Modifier
            .height(24.dp)
            .background(if (tinted) color.copy(alpha = 0.12f) else Color.Transparent, CircleShape)
            .border(BorderStroke(1.dp, if (tinted) color.copy(alpha = 0.4f) else MaterialTheme.colorScheme.outline), CircleShape)
            .padding(horizontal = 8.dp),
        verticalAlignment = Alignment.CenterVertically,
        horizontalArrangement = Arrangement.spacedBy(5.dp),
    ) {
        if (tinted) Box(Modifier.size(6.dp).background(color, CircleShape))
        Text(text = text.uppercase(), style = MaterialTheme.typography.labelSmall.copy(fontSize = 10.5.sp), color = color, maxLines = 1)
    }
}

@Composable
private fun ReconnectingIndicator(modifier: Modifier = Modifier) {
    Row(modifier, verticalAlignment = Alignment.CenterVertically) {
        CircularProgressIndicator(modifier = Modifier.size(14.dp), color = ThePreyTheme.colors.caution, strokeWidth = 2.dp)
        Spacer(Modifier.width(8.dp))
        Text(
            text = stringResource(R.string.lobby_reconnecting).uppercase(),
            style = MaterialTheme.typography.labelSmall,
            color = ThePreyTheme.colors.caution,
        )
    }
}

@Composable
private fun ActionBar(lobby: LobbyUiState, onSetReady: () -> Unit, onStart: () -> Unit) {
    Column(
        Modifier
            .fillMaxWidth()
            .background(MaterialTheme.colorScheme.background)
            .navigationBarsPadding()
            .padding(16.dp),
        verticalArrangement = Arrangement.spacedBy(10.dp),
    ) {
        if (lobby.isOwner) {
            lobby.startHint?.let { hint ->
                Text(
                    text = stringResource(
                        when (hint) {
                            StartHint.WaitingForPlayers -> R.string.lobby_hint_waiting_for_players
                            StartHint.ChooseHunter -> R.string.lobby_hint_choose_hunter
                            StartHint.PlayersGettingReady -> R.string.lobby_hint_getting_ready
                        },
                    ),
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                    textAlign = TextAlign.Center,
                    modifier = Modifier.fillMaxWidth(),
                )
            }
            PrimaryButton(
                text = stringResource(R.string.lobby_start),
                busyText = stringResource(R.string.lobby_starting),
                onClick = onStart,
                enabled = lobby.canStart,
                busy = lobby.pending == LobbyCommand.Start,
            )
        } else {
            val ready = lobby.rows.firstOrNull { it.isMe }?.isReady == true
            PrimaryButton(
                text = stringResource(if (ready) R.string.lobby_you_are_ready else R.string.lobby_set_ready),
                onClick = onSetReady,
                enabled = lobby.canSetReady,
                busy = lobby.pending == LobbyCommand.Ready,
            )
        }
    }
}

@Composable
private fun LeaveDialog(isOwner: Boolean, onConfirm: () -> Unit, onDismiss: () -> Unit) {
    AlertDialog(
        onDismissRequest = onDismiss,
        containerColor = MaterialTheme.colorScheme.surface,
        titleContentColor = MaterialTheme.colorScheme.onSurface,
        textContentColor = MaterialTheme.colorScheme.onSurfaceVariant,
        title = { Text(stringResource(R.string.lobby_leave_title), style = MaterialTheme.typography.headlineSmall) },
        text = {
            Text(
                stringResource(if (isOwner) R.string.lobby_leave_body_owner else R.string.lobby_leave_body),
                style = MaterialTheme.typography.bodyMedium,
            )
        },
        confirmButton = {
            TextButton(onClick = onConfirm) {
                Text(
                    text = stringResource(if (isOwner) R.string.lobby_leave_cancel_game else R.string.lobby_leave_confirm).uppercase(),
                    style = MaterialTheme.typography.labelLarge,
                    color = ThePreyTheme.colors.hunter,
                )
            }
        },
        dismissButton = {
            TextButton(onClick = onDismiss) {
                Text(
                    text = stringResource(R.string.lobby_stay).uppercase(),
                    style = MaterialTheme.typography.labelLarge,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )
            }
        },
    )
}

// ---- Previews ----

private fun previewLobby(isOwner: Boolean, ready: Boolean = false) = LobbyUiState(
    gameCode = "4291",
    isOwner = isOwner,
    settings = GameSettings(duration = 60),
    rows = listOf(
        LobbyPlayerRow("o", "Night-Hawk_7", isHunter = false, isReady = true, isMe = isOwner),
        LobbyPlayerRow("a", "Vos", isHunter = true, isReady = ready, isMe = !isOwner),
        LobbyPlayerRow("b", "Ghost-Runner", isHunter = false, isReady = false, isMe = false),
    ),
    readyCount = if (ready) 2 else 1,
    canEdit = isOwner,
    canSetReady = !isOwner && !ready,
    canStart = false,
    startHint = if (isOwner) StartHint.PlayersGettingReady else null,
    pending = null,
    reconnecting = false,
)

@Composable
private fun ContentPreview(state: LobbyScreenState) {
    ThePreyTheme {
        LobbyContent(
            state = state,
            snackbarHostState = remember { SnackbarHostState() },
            onBack = {},
            onRetry = {},
            onShare = {},
            onSettingChange = { _, _ -> },
            onPlayerTap = {},
            onSetReady = {},
            onStart = {},
        )
    }
}

@Preview(name = "Owner", heightDp = 1400)
@Composable
private fun OwnerPreview() = ContentPreview(LobbyScreenState.Content(previewLobby(isOwner = true)))

@Preview(name = "Player (Dutch)", locale = "nl", heightDp = 1000)
@Composable
private fun PlayerPreview() = ContentPreview(LobbyScreenState.Content(previewLobby(isOwner = false).copy(reconnecting = true)))

@Preview(name = "Player ready")
@Composable
private fun PlayerReadyPreview() = ContentPreview(LobbyScreenState.Content(previewLobby(isOwner = false, ready = true)))

@Preview(name = "Error")
@Composable
private fun ErrorPreview() = ContentPreview(LobbyScreenState.Error)
