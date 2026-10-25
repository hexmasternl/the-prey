package nl.hexmaster.theprey.ui.mainmenu

import androidx.activity.compose.LocalActivity
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.BoxWithConstraints
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.WindowInsets
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.safeDrawing
import androidx.compose.foundation.layout.widthIn
import androidx.compose.foundation.layout.windowInsetsPadding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.semantics.LiveRegionMode
import androidx.compose.ui.semantics.liveRegion
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.semantics.stateDescription
import androidx.compose.ui.text.SpanStyle
import androidx.compose.ui.text.buildAnnotatedString
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.text.withStyle
import androidx.compose.ui.tooling.preview.Preview
import androidx.compose.ui.unit.Dp
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.lifecycle.Lifecycle
import androidx.lifecycle.compose.LifecycleEventEffect
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import nl.hexmaster.theprey.R
import nl.hexmaster.theprey.data.location.GeoPosition
import nl.hexmaster.theprey.data.location.LocationAccess
import nl.hexmaster.theprey.data.location.readLocationAccess
import nl.hexmaster.theprey.ui.components.PrimaryButton
import nl.hexmaster.theprey.ui.components.PrimaryButtonStyle
import nl.hexmaster.theprey.ui.components.RunningLightText
import nl.hexmaster.theprey.ui.components.SecondaryButton
import nl.hexmaster.theprey.ui.components.TacticalMapBackground
import nl.hexmaster.theprey.ui.components.formatPosition
import nl.hexmaster.theprey.ui.components.rememberReducedMotion
import nl.hexmaster.theprey.ui.consent.LocalLocationConsent
import nl.hexmaster.theprey.ui.consent.openAppSettings
import nl.hexmaster.theprey.ui.theme.ThePreyTheme

@Composable
fun MainMenuScreen(
    viewModel: MainMenuViewModel,
    onPlayNow: () -> Unit,
    onResumeGame: (gameId: String) -> Unit,
    onPlayfields: () -> Unit,
    onSettings: () -> Unit,
    modifier: Modifier = Modifier,
) {
    val uiState by viewModel.uiState.collectAsStateWithLifecycle()
    val context = LocalContext.current
    val activity = LocalActivity.current

    // The consent host owns the permission prompt; the menu never prompts, it only reads. The
    // lifecycle owner is the menu's back-stack entry, so this runs on first show, on return from a
    // sub-screen and on return from the system settings.
    LifecycleEventEffect(Lifecycle.Event.ON_RESUME) {
        viewModel.onLocationAccess(readLocationAccess(context))
        viewModel.refreshActiveGame()
    }
    // A grant through the consent dialog while the menu stays resumed: read the permissions again.
    val consent = LocalLocationConsent.current
    LaunchedEffect(consent.granted, consent.canReadLocation) {
        viewModel.onLocationAccess(readLocationAccess(context))
    }

    MainMenuContent(
        uiState = uiState,
        onPlayNow = onPlayNow,
        onResumeGame = onResumeGame,
        onOpenSettings = { context.openAppSettings() },
        onRetry = viewModel::refreshActiveGame,
        onPlayfields = onPlayfields,
        onSettings = onSettings,
        onSignOut = { viewModel.signOut(context) },
        // Closes the app without signing out; the next launch restores the session.
        onQuit = { activity?.finishAndRemoveTask() },
        modifier = modifier,
    )
}

@Composable
fun MainMenuContent(
    uiState: MainMenuUiState,
    onPlayNow: () -> Unit,
    onResumeGame: (gameId: String) -> Unit,
    onOpenSettings: () -> Unit,
    onRetry: () -> Unit,
    onPlayfields: () -> Unit,
    onSettings: () -> Unit,
    onSignOut: () -> Unit,
    onQuit: () -> Unit,
    modifier: Modifier = Modifier,
    animate: Boolean = !rememberReducedMotion(),
) {
    Box(modifier.fillMaxSize().background(MaterialTheme.colorScheme.background)) {
        TacticalMapBackground(Modifier.matchParentSize(), animate = animate)
        BoxWithConstraints(Modifier.fillMaxSize().windowInsetsPadding(WindowInsets.safeDrawing)) {
            // The window the menu actually has, not the device orientation: covers split screen too.
            if (maxWidth > maxHeight) {
                LandscapeMenu(
                    uiState = uiState,
                    minHeight = maxHeight,
                    animate = animate,
                    actions = {
                        MenuActions(
                            uiState = uiState,
                            onPlayNow = onPlayNow,
                            onResumeGame = onResumeGame,
                            onOpenSettings = onOpenSettings,
                            onRetry = onRetry,
                            onPlayfields = onPlayfields,
                            onSettings = onSettings,
                            onSignOut = onSignOut,
                            onQuit = onQuit,
                        )
                    },
                )
                return@BoxWithConstraints
            }
            val compact = maxWidth < 360.dp
            Column(
                modifier = Modifier
                    .fillMaxWidth()
                    .verticalScroll(rememberScrollState())
                    // Fill the screen so the sections spread out; scroll only when they don't fit.
                    .heightIn(min = maxHeight)
                    .padding(horizontal = 28.dp, vertical = 20.dp),
                verticalArrangement = Arrangement.SpaceBetween,
            ) {
                Readout(uiState)
                Slogan(compact, Modifier.padding(vertical = 40.dp))
                Column(horizontalAlignment = Alignment.CenterHorizontally) {
                    MenuActions(
                        uiState = uiState,
                        onPlayNow = onPlayNow,
                        onResumeGame = onResumeGame,
                        onOpenSettings = onOpenSettings,
                        onRetry = onRetry,
                        onPlayfields = onPlayfields,
                        onSettings = onSettings,
                        onSignOut = onSignOut,
                        onQuit = onQuit,
                    )
                    PlayerName(uiState.playerName, animate)
                }
            }
        }
    }
}

/**
 * Landscape arrangement: the readout across the top, the slogan left and the actions right, and the
 * player name centred across the full width at the bottom. Like portrait, the whole column fills the
 * screen and scrolls as one when it doesn't fit, so the name never covers the buttons.
 */
@Composable
private fun LandscapeMenu(
    uiState: MainMenuUiState,
    minHeight: Dp,
    animate: Boolean,
    actions: @Composable () -> Unit,
) {
    Column(
        modifier = Modifier
            .fillMaxWidth()
            .verticalScroll(rememberScrollState())
            .heightIn(min = minHeight)
            .padding(horizontal = 28.dp, vertical = 20.dp),
        verticalArrangement = Arrangement.SpaceBetween,
    ) {
        Readout(uiState)
        Row(
            modifier = Modifier.fillMaxWidth().padding(vertical = 20.dp),
            horizontalArrangement = Arrangement.spacedBy(32.dp),
            verticalAlignment = Alignment.CenterVertically,
        ) {
            // The left column is about half the width, so size the slogan by that column.
            BoxWithConstraints(Modifier.weight(1f), contentAlignment = Alignment.Center) {
                Slogan(compact = maxWidth < 360.dp)
            }
            Box(Modifier.weight(1f), contentAlignment = Alignment.Center) {
                actions()
            }
        }
        Column(Modifier.fillMaxWidth(), horizontalAlignment = Alignment.CenterHorizontally) {
            PlayerName(uiState.playerName, animate)
        }
    }
}

/** The player's display name with its running-light sweep, centred under the actions. */
@Composable
private fun PlayerName(name: String, animate: Boolean) {
    Spacer(Modifier.height(28.dp))
    RunningLightText(
        text = name.uppercase(),
        style = MaterialTheme.typography.titleMedium.copy(letterSpacing = 3.sp),
        // Dim at rest so the green sweep stands out.
        color = ThePreyTheme.colors.textGhost,
        animate = animate,
    )
    Spacer(Modifier.height(12.dp))
}

@Composable
private fun Readout(uiState: MainMenuUiState) {
    Column(Modifier.fillMaxWidth()) {
        Text(
            text = positionText(uiState.position).uppercase(),
            modifier = Modifier.fillMaxWidth(),
            style = MaterialTheme.typography.labelSmall,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
            textAlign = TextAlign.End,
        )
        Spacer(Modifier.height(12.dp))
        Text(
            text = stringResource(R.string.main_menu_field_manual, uiState.appVersion).uppercase(),
            style = MaterialTheme.typography.labelSmall.copy(letterSpacing = 3.sp),
            color = MaterialTheme.colorScheme.secondary,
        )
    }
}

@Composable
private fun positionText(position: GeoPosition?): String {
    if (position == null) {
        return stringResource(R.string.main_menu_position_unknown, stringResource(R.string.main_menu_no_signal))
    }
    val (latitude, longitude) = formatPosition(position)
    return stringResource(
        R.string.main_menu_position,
        latitude.degrees,
        latitude.hemisphere,
        longitude.degrees,
        longitude.hemisphere,
    )
}

@Composable
private fun Slogan(compact: Boolean, modifier: Modifier = Modifier) {
    val text = MaterialTheme.colorScheme.onBackground
    val accent = MaterialTheme.colorScheme.primary
    val slogan = buildAnnotatedString {
        withStyle(SpanStyle(color = text)) { append(stringResource(R.string.main_menu_slogan_line1_a).uppercase()) }
        append(' ')
        withStyle(SpanStyle(color = accent)) { append(stringResource(R.string.main_menu_slogan_line1_b).uppercase()) }
        append('\n')
        withStyle(SpanStyle(color = text)) { append(stringResource(R.string.main_menu_slogan_line2_a).uppercase()) }
        append(' ')
        withStyle(SpanStyle(color = accent)) { append(stringResource(R.string.main_menu_slogan_line2_b).uppercase()) }
    }
    Column(modifier.widthIn(max = 480.dp)) {
        Text(
            text = slogan,
            style = if (compact) MaterialTheme.typography.displaySmall else MaterialTheme.typography.displayMedium,
        )
        Spacer(Modifier.height(24.dp))
        Text(
            text = stringResource(R.string.main_menu_description),
            style = MaterialTheme.typography.bodyLarge,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )
    }
}

@Composable
private fun MenuActions(
    uiState: MainMenuUiState,
    onPlayNow: () -> Unit,
    onResumeGame: (gameId: String) -> Unit,
    onOpenSettings: () -> Unit,
    onRetry: () -> Unit,
    onPlayfields: () -> Unit,
    onSettings: () -> Unit,
    onSignOut: () -> Unit,
    onQuit: () -> Unit,
) {
    Column(Modifier.widthIn(max = 480.dp), verticalArrangement = Arrangement.spacedBy(12.dp)) {
        PlayButton(uiState.playAction, onPlayNow, onResumeGame)
        uiState.playAction.error?.let { error ->
            PlayActionNotice(error, onOpenSettings = onOpenSettings, onRetry = onRetry)
        }
        SecondaryButton(text = stringResource(R.string.main_menu_playfields), onClick = onPlayfields, height = 48.dp)
        SecondaryButton(text = stringResource(R.string.main_menu_settings), onClick = onSettings, height = 48.dp)
        SecondaryButton(
            text = stringResource(R.string.main_menu_sign_out),
            onClick = onSignOut,
            busy = uiState.isSigningOut,
            height = 48.dp,
        )
        SecondaryButton(text = stringResource(R.string.main_menu_quit), onClick = onQuit, height = 48.dp)
    }
}

@Composable
private fun PlayButton(play: PlayActionState, onPlayNow: () -> Unit, onResumeGame: (gameId: String) -> Unit) {
    val resumeGameId = play.gameId.takeIf { play.label == PlayActionLabel.ResumeGame }
    // A disabled button alone tells screen-reader users nothing: say why it is unavailable.
    val reason = if (play.enabled) {
        null
    } else {
        stringResource(
            when (play.error) {
                null -> R.string.main_menu_play_state_checking
                PlayActionError.LocationMissing -> R.string.main_menu_play_state_location_missing
                PlayActionError.BackgroundLocationMissing -> R.string.main_menu_play_state_background_location_missing
                PlayActionError.CheckFailed -> R.string.main_menu_play_state_check_failed
            },
        )
    }
    PrimaryButton(
        text = stringResource(if (resumeGameId != null) R.string.main_menu_resume_game else R.string.main_menu_play_now),
        onClick = { if (resumeGameId != null) onResumeGame(resumeGameId) else onPlayNow() },
        enabled = play.enabled,
        style = if (play.style == PlayActionStyle.Hunter) PrimaryButtonStyle.Hunter else PrimaryButtonStyle.Signal,
        modifier = if (reason != null) Modifier.semantics { stateDescription = reason } else Modifier,
    )
}

/** What blocks the play button and how to fix it, right below the button. */
@Composable
private fun PlayActionNotice(error: PlayActionError, onOpenSettings: () -> Unit, onRetry: () -> Unit) {
    val message = when (error) {
        PlayActionError.LocationMissing -> R.string.main_menu_error_location_missing
        PlayActionError.BackgroundLocationMissing -> R.string.main_menu_error_background_location_missing
        PlayActionError.CheckFailed -> R.string.main_menu_error_check_failed
    }
    Column(
        Modifier.fillMaxWidth().semantics { liveRegion = LiveRegionMode.Polite },
        verticalArrangement = Arrangement.spacedBy(8.dp),
    ) {
        Text(
            text = stringResource(message),
            style = MaterialTheme.typography.labelMedium,
            color = ThePreyTheme.colors.hunter,
        )
        if (error == PlayActionError.CheckFailed) {
            SecondaryButton(text = stringResource(R.string.main_menu_retry), onClick = onRetry, height = 48.dp)
        } else {
            SecondaryButton(text = stringResource(R.string.main_menu_open_settings), onClick = onOpenSettings, height = 48.dp)
        }
    }
}

@Composable
private fun MainMenuPreview(uiState: MainMenuUiState) {
    ThePreyTheme {
        MainMenuContent(
            uiState = uiState,
            onPlayNow = {},
            onResumeGame = {},
            onOpenSettings = {},
            onRetry = {},
            onPlayfields = {},
            onSettings = {},
            onSignOut = {},
            onQuit = {},
            animate = false,
        )
    }
}

private val previewState = MainMenuUiState(playerName = "Night-Hawk_7", appVersion = "1.0.0")

@Preview(widthDp = 360, heightDp = 800)
@Composable
private fun MainMenuLoadingPreview() {
    MainMenuPreview(previewState.copy(playAction = playAction(LocationAccess.Granted, ActiveGameCheck.Loading)))
}

@Preview(widthDp = 360, heightDp = 800)
@Composable
private fun MainMenuPlayNowPreview() {
    MainMenuPreview(
        previewState.copy(
            position = GeoPosition(52.36761, 4.90412),
            playAction = playAction(LocationAccess.Granted, ActiveGameCheck.None),
        ),
    )
}

@Preview(widthDp = 360, heightDp = 800)
@Composable
private fun MainMenuResumeGamePreview() {
    MainMenuPreview(previewState.copy(playAction = playAction(LocationAccess.Granted, ActiveGameCheck.Active("game-1"))))
}

@Preview(widthDp = 360, heightDp = 900)
@Composable
private fun MainMenuBackgroundLocationMissingPreview() {
    MainMenuPreview(previewState.copy(playAction = playAction(LocationAccess.ForegroundOnly, ActiveGameCheck.None)))
}

@Preview(widthDp = 360, heightDp = 900)
@Composable
private fun MainMenuCheckFailedPreview() {
    MainMenuPreview(previewState.copy(playAction = playAction(LocationAccess.Granted, ActiveGameCheck.Failed)))
}

@Preview(widthDp = 800, heightDp = 360)
@Composable
private fun MainMenuLandscapePreview() {
    MainMenuPreview(
        previewState.copy(
            position = GeoPosition(52.36761, 4.90412),
            playAction = playAction(LocationAccess.Granted, ActiveGameCheck.None),
        ),
    )
}

@Preview(widthDp = 800, heightDp = 360)
@Composable
private fun MainMenuLandscapeBlockedPreview() {
    MainMenuPreview(previewState.copy(playAction = playAction(LocationAccess.ForegroundOnly, ActiveGameCheck.None)))
}

@Preview(widthDp = 800, heightDp = 360, locale = "nl")
@Composable
private fun MainMenuLandscapeDutchPreview() {
    MainMenuPreview(
        previewState.copy(
            position = GeoPosition(52.36761, 4.90412),
            playAction = playAction(LocationAccess.Granted, ActiveGameCheck.None),
        ),
    )
}
