package nl.hexmaster.theprey.ui.mainmenu

import android.Manifest
import android.content.pm.PackageManager
import androidx.activity.compose.LocalActivity
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.BoxWithConstraints
import androidx.compose.foundation.layout.Column
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
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.SpanStyle
import androidx.compose.ui.text.buildAnnotatedString
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.text.withStyle
import androidx.compose.ui.tooling.preview.Preview
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.core.content.ContextCompat
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import nl.hexmaster.theprey.R
import nl.hexmaster.theprey.data.location.GeoPosition
import nl.hexmaster.theprey.ui.components.PrimaryButton
import nl.hexmaster.theprey.ui.components.RunningLightText
import nl.hexmaster.theprey.ui.components.SecondaryButton
import nl.hexmaster.theprey.ui.components.TacticalMapBackground
import nl.hexmaster.theprey.ui.components.rememberReducedMotion
import nl.hexmaster.theprey.ui.theme.ThePreyTheme

private val LocationPermissions = arrayOf(
    Manifest.permission.ACCESS_FINE_LOCATION,
    Manifest.permission.ACCESS_COARSE_LOCATION,
)

@Composable
fun MainMenuScreen(
    viewModel: MainMenuViewModel,
    onPlayNow: () -> Unit,
    onPlayfields: () -> Unit,
    onSettings: () -> Unit,
    modifier: Modifier = Modifier,
) {
    val uiState by viewModel.uiState.collectAsStateWithLifecycle()
    val context = LocalContext.current
    val activity = LocalActivity.current

    val permissionLauncher = rememberLauncherForActivityResult(ActivityResultContracts.RequestMultiplePermissions()) { grants ->
        // Either precision is enough for the readout.
        viewModel.onLocationPermissionResult(grants.values.any { it })
    }
    // Ask once per menu visit; a denial is not repeated when returning from a sub-screen.
    var permissionAsked by rememberSaveable { mutableStateOf(false) }
    LaunchedEffect(Unit) {
        val granted = LocationPermissions.any {
            ContextCompat.checkSelfPermission(context, it) == PackageManager.PERMISSION_GRANTED
        }
        when {
            granted -> viewModel.onLocationPermissionResult(true)
            !permissionAsked -> {
                permissionAsked = true
                permissionLauncher.launch(LocationPermissions)
            }
        }
    }

    MainMenuContent(
        uiState = uiState,
        onPlayNow = onPlayNow,
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
                    MenuActions(uiState, onPlayNow, onPlayfields, onSettings, onSignOut, onQuit)
                    Spacer(Modifier.height(28.dp))
                    RunningLightText(
                        text = uiState.playerName.uppercase(),
                        style = MaterialTheme.typography.titleMedium.copy(letterSpacing = 3.sp),
                        animate = animate,
                    )
                    Spacer(Modifier.height(12.dp))
                }
            }
        }
    }
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
    onPlayfields: () -> Unit,
    onSettings: () -> Unit,
    onSignOut: () -> Unit,
    onQuit: () -> Unit,
) {
    Column(Modifier.widthIn(max = 480.dp), verticalArrangement = Arrangement.spacedBy(12.dp)) {
        PrimaryButton(text = stringResource(R.string.main_menu_play_now), onClick = onPlayNow)
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

@Preview(widthDp = 360, heightDp = 800)
@Composable
private fun MainMenuNoSignalPreview() {
    ThePreyTheme {
        MainMenuContent(
            uiState = MainMenuUiState(playerName = "Night-Hawk_7", appVersion = "1.0.0"),
            onPlayNow = {},
            onPlayfields = {},
            onSettings = {},
            onSignOut = {},
            onQuit = {},
            animate = false,
        )
    }
}

@Preview(widthDp = 360, heightDp = 800)
@Composable
private fun MainMenuWithPositionPreview() {
    ThePreyTheme {
        MainMenuContent(
            uiState = MainMenuUiState(
                playerName = "Night-Hawk_7",
                appVersion = "1.0.0",
                position = GeoPosition(52.36761, 4.90412),
            ),
            onPlayNow = {},
            onPlayfields = {},
            onSettings = {},
            onSignOut = {},
            onQuit = {},
            animate = false,
        )
    }
}
