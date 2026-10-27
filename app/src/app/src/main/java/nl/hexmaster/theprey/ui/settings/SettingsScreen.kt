package nl.hexmaster.theprey.ui.settings

import android.Manifest
import android.app.Activity
import android.os.Build
import androidx.activity.compose.LocalActivity
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.WindowInsets
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.safeDrawing
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.layout.widthIn
import androidx.compose.foundation.layout.windowInsetsPadding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.selection.selectable
import androidx.compose.foundation.selection.selectableGroup
import androidx.compose.foundation.selection.toggleable
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.KeyboardActions
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.OutlinedTextFieldDefaults
import androidx.compose.material3.RadioButton
import androidx.compose.material3.RadioButtonDefaults
import androidx.compose.material3.SnackbarHost
import androidx.compose.material3.SnackbarHostState
import androidx.compose.material3.Surface
import androidx.compose.material3.Switch
import androidx.compose.material3.SwitchDefaults
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.platform.LocalResources
import androidx.compose.ui.platform.LocalUriHandler
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.semantics.LiveRegionMode
import androidx.compose.ui.semantics.Role
import androidx.compose.ui.semantics.heading
import androidx.compose.ui.semantics.liveRegion
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.text.input.ImeAction
import androidx.compose.ui.text.input.KeyboardCapitalization
import androidx.compose.ui.tooling.preview.Preview
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.core.app.ActivityCompat
import androidx.lifecycle.Lifecycle
import androidx.lifecycle.compose.LifecycleEventEffect
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import kotlinx.coroutines.launch
import nl.hexmaster.theprey.R
import nl.hexmaster.theprey.data.language.LanguageChoice
import nl.hexmaster.theprey.data.location.LocationAccess
import nl.hexmaster.theprey.data.location.LocationConsentStore
import nl.hexmaster.theprey.data.location.notificationsAllowed
import nl.hexmaster.theprey.data.location.readLocationAccess
import nl.hexmaster.theprey.ui.components.PrimaryButton
import nl.hexmaster.theprey.ui.components.SecondaryButton
import nl.hexmaster.theprey.ui.consent.LocationDisclosure
import nl.hexmaster.theprey.ui.consent.currentPrivacyPolicyUrl
import nl.hexmaster.theprey.ui.consent.openAppSettings
import nl.hexmaster.theprey.ui.theme.ThePreyTheme

/** What the Location section shows: the permissions as last read, and what Grant would do. */
data class LocationSectionState(
    val access: LocationAccess,
    /** Null below Android 13, where notifications need no permission and the row is hidden. */
    val notificationsAllowed: Boolean?,
    val step: GrantStep,
)

private const val FINE = Manifest.permission.ACCESS_FINE_LOCATION
private const val COARSE = Manifest.permission.ACCESS_COARSE_LOCATION

/** `r-md` from the design system: cards and alerts. */
private val PanelShape = RoundedCornerShape(12.dp)

/**
 * The Settings screen. Location permissions are read on every resume of the back-stack entry
 * (also after returning from the system settings); the system prompt only ever runs from the
 * Grant action under the always-visible disclosure, never on opening the screen.
 */
@Composable
fun SettingsScreen(
    viewModel: SettingsViewModel,
    consentStore: LocationConsentStore,
    onBack: () -> Unit,
    modifier: Modifier = Modifier,
) {
    val uiState by viewModel.uiState.collectAsStateWithLifecycle()
    val context = LocalContext.current
    val activity = LocalActivity.current
    val uriHandler = LocalUriHandler.current
    val privacyUrl = currentPrivacyPolicyUrl()
    val scope = rememberCoroutineScope()
    val disclosureAccepted by consentStore.disclosureAccepted.collectAsStateWithLifecycle(initialValue = false)

    var access by remember { mutableStateOf(readLocationAccess(context)) }
    var notifications by remember { mutableStateOf(notificationsAllowed(context)) }
    var rationale by remember { mutableStateOf(activity.shouldExplainFine()) }
    val refresh = {
        access = readLocationAccess(context)
        notifications = notificationsAllowed(context)
        rationale = activity.shouldExplainFine()
    }

    val notificationLauncher = rememberLauncherForActivityResult(ActivityResultContracts.RequestPermission()) {
        refresh()
    }
    val locationLauncher = rememberLauncherForActivityResult(ActivityResultContracts.RequestMultiplePermissions()) {
        refresh()
        // On Android 13+ the tracking notification needs its own permission, asked after location.
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU && !notifications) {
            notificationLauncher.launch(Manifest.permission.POST_NOTIFICATIONS)
        }
    }
    LifecycleEventEffect(Lifecycle.Event.ON_RESUME) { refresh() }

    val location = LocationSectionState(
        access = access,
        notificationsAllowed = notifications.takeIf { Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU },
        step = grantStep(access, canAskFine(rationale = rationale, disclosureAccepted = disclosureAccepted)),
    )

    val snackbarHostState = remember { SnackbarHostState() }
    val resources = LocalResources.current
    LaunchedEffect(viewModel) {
        viewModel.hintsResetMessages.collect { message ->
            val text = when (message) {
                HintsResetMessage.Done -> R.string.settings_hints_reset_done
                HintsResetMessage.Failed -> R.string.settings_hints_reset_failed
            }
            snackbarHostState.showSnackbar(resources.getString(text))
        }
    }

    Box(modifier.fillMaxSize()) {
        SettingsContent(
            uiState = uiState,
            location = location,
            onBack = onBack,
            onCallsignChange = viewModel::onCallsignChange,
            onSave = viewModel::onSave,
            onLanguageSelected = viewModel::onLanguageSelected,
            onKeepScreenOnChange = viewModel::onKeepScreenOnChange,
            onPrivacyPolicy = { uriHandler.openUri(privacyUrl) },
            onGrant = {
                when (location.step) {
                    GrantStep.RequestPrecise -> scope.launch {
                        // The panel right above the button is the disclosure: record it before the prompt.
                        consentStore.setDisclosureAccepted(true)
                        locationLauncher.launch(arrayOf(FINE, COARSE))
                    }
                    GrantStep.OpenSettingsForPrecise, GrantStep.OpenSettingsForAllTheTime -> context.openAppSettings()
                    GrantStep.None -> Unit
                }
            },
            onResetHints = viewModel::resetHints,
        )
        SnackbarHost(
            snackbarHostState,
            Modifier.align(Alignment.BottomCenter).windowInsetsPadding(WindowInsets.safeDrawing),
        )
    }
}

/** Whether the system would show the fine-location prompt again after an earlier denial. */
private fun Activity?.shouldExplainFine(): Boolean =
    this != null && ActivityCompat.shouldShowRequestPermissionRationale(this, FINE)

@Composable
fun SettingsContent(
    uiState: SettingsUiState,
    location: LocationSectionState,
    onBack: () -> Unit,
    onCallsignChange: (String) -> Unit,
    onSave: () -> Unit,
    onLanguageSelected: (LanguageChoice) -> Unit,
    onPrivacyPolicy: () -> Unit,
    onGrant: () -> Unit,
    onKeepScreenOnChange: (Boolean) -> Unit = {},
    onResetHints: () -> Unit = {},
    modifier: Modifier = Modifier,
) {
    Column(
        modifier
            .fillMaxSize()
            .background(MaterialTheme.colorScheme.background)
            .windowInsetsPadding(WindowInsets.safeDrawing),
    ) {
        Row(Modifier.padding(horizontal = 4.dp, vertical = 8.dp), verticalAlignment = Alignment.CenterVertically) {
            IconButton(onClick = onBack) {
                Icon(
                    painter = painterResource(R.drawable.ic_arrow_back),
                    contentDescription = stringResource(R.string.navigate_back),
                    tint = MaterialTheme.colorScheme.onBackground,
                )
            }
            Spacer(Modifier.width(4.dp))
            Text(
                text = stringResource(R.string.settings_title),
                style = MaterialTheme.typography.headlineSmall,
                color = MaterialTheme.colorScheme.onBackground,
                modifier = Modifier.semantics { heading() },
            )
        }
        Column(
            Modifier
                .fillMaxWidth()
                .verticalScroll(rememberScrollState())
                .padding(horizontal = 16.dp)
                .padding(bottom = 24.dp),
            horizontalAlignment = Alignment.CenterHorizontally,
        ) {
            Column(Modifier.widthIn(max = 560.dp).fillMaxWidth(), verticalArrangement = Arrangement.spacedBy(32.dp)) {
                CallsignSection(uiState, onCallsignChange, onSave)
                LanguageSection(uiState, onLanguageSelected)
                DisplaySection(uiState.keepScreenOn, onKeepScreenOnChange)
                LocationSection(location, onPrivacyPolicy, onGrant)
                HintsSection(onResetHints)
            }
        }
    }
}

@Composable
private fun SectionTitle(text: String) {
    Text(
        text = text.uppercase(),
        style = MaterialTheme.typography.labelSmall.copy(letterSpacing = 3.sp),
        color = MaterialTheme.colorScheme.secondary,
        modifier = Modifier.semantics { heading() },
    )
}

@Composable
private fun SectionCard(content: @Composable () -> Unit) {
    Surface(
        modifier = Modifier.fillMaxWidth(),
        shape = PanelShape,
        color = MaterialTheme.colorScheme.surface,
        contentColor = MaterialTheme.colorScheme.onSurface,
        border = BorderStroke(1.dp, MaterialTheme.colorScheme.outlineVariant),
    ) {
        Column(Modifier.padding(16.dp)) { content() }
    }
}

@Composable
private fun CallsignSection(uiState: SettingsUiState, onCallsignChange: (String) -> Unit, onSave: () -> Unit) {
    Column(verticalArrangement = Arrangement.spacedBy(12.dp)) {
        SectionTitle(stringResource(R.string.settings_section_callsign))
        SectionCard {
            val rejected = uiState.saveStatus == CallsignSaveStatus.Rejected
            val invalid = uiState.check == CallsignCheck.Invalid
            val error = invalid || rejected
            Text(
                text = stringResource(R.string.settings_callsign_label).uppercase(),
                style = MaterialTheme.typography.labelMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
            Spacer(Modifier.height(8.dp))
            OutlinedTextField(
                value = uiState.callsign,
                onValueChange = onCallsignChange,
                modifier = Modifier.fillMaxWidth(),
                singleLine = true,
                isError = error,
                enabled = !uiState.saving,
                textStyle = MaterialTheme.typography.bodyLarge,
                keyboardOptions = KeyboardOptions(capitalization = KeyboardCapitalization.None, imeAction = ImeAction.Done),
                keyboardActions = KeyboardActions(onDone = { if (uiState.canSave) onSave() }),
                shape = MaterialTheme.shapes.small,
                colors = OutlinedTextFieldDefaults.colors(
                    focusedContainerColor = MaterialTheme.colorScheme.surfaceContainerLowest,
                    unfocusedContainerColor = MaterialTheme.colorScheme.surfaceContainerLowest,
                    disabledContainerColor = MaterialTheme.colorScheme.surfaceContainerLowest,
                    errorContainerColor = MaterialTheme.colorScheme.surfaceContainerLowest,
                    focusedBorderColor = MaterialTheme.colorScheme.primary,
                    unfocusedBorderColor = MaterialTheme.colorScheme.outline,
                    errorBorderColor = MaterialTheme.colorScheme.error,
                    cursorColor = MaterialTheme.colorScheme.primary,
                    focusedTextColor = MaterialTheme.colorScheme.onSurface,
                    unfocusedTextColor = MaterialTheme.colorScheme.onSurface,
                    disabledTextColor = MaterialTheme.colorScheme.onSurfaceVariant,
                    errorTextColor = MaterialTheme.colorScheme.onSurface,
                ),
            )
            Spacer(Modifier.height(8.dp))
            Text(
                text = stringResource(
                    when {
                        rejected -> R.string.settings_callsign_rejected
                        invalid -> R.string.settings_callsign_invalid
                        else -> R.string.settings_callsign_rule
                    },
                ),
                style = MaterialTheme.typography.bodySmall,
                color = if (error) MaterialTheme.colorScheme.error else MaterialTheme.colorScheme.onSurfaceVariant,
                modifier = if (error) Modifier.semantics { liveRegion = LiveRegionMode.Polite } else Modifier,
            )
            if (uiState.currentCallsign.isBlank()) {
                Spacer(Modifier.height(8.dp))
                Text(
                    text = stringResource(R.string.settings_callsign_no_callsign, uiState.displayName),
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )
            }
            Spacer(Modifier.height(16.dp))
            PrimaryButton(
                text = stringResource(R.string.settings_callsign_save),
                onClick = onSave,
                enabled = uiState.canSave,
                busy = uiState.saving,
                height = 48.dp,
            )
            val status = when (uiState.saveStatus) {
                CallsignSaveStatus.Saved -> R.string.settings_callsign_saved to MaterialTheme.colorScheme.primary
                CallsignSaveStatus.Failed -> R.string.settings_callsign_failed to MaterialTheme.colorScheme.error
                CallsignSaveStatus.Idle, CallsignSaveStatus.Rejected -> null
            }
            if (status != null) {
                Spacer(Modifier.height(12.dp))
                Text(
                    text = stringResource(status.first),
                    style = MaterialTheme.typography.labelMedium,
                    color = status.second,
                    modifier = Modifier.semantics { liveRegion = LiveRegionMode.Polite },
                )
            }
        }
    }
}

@Composable
private fun LanguageSection(uiState: SettingsUiState, onLanguageSelected: (LanguageChoice) -> Unit) {
    Column(verticalArrangement = Arrangement.spacedBy(12.dp)) {
        SectionTitle(stringResource(R.string.settings_section_language))
        SectionCard {
            Column(Modifier.selectableGroup()) {
                LanguageChoice.entries.forEach { choice ->
                    val label = stringResource(
                        when (choice) {
                            LanguageChoice.System -> R.string.settings_language_system
                            LanguageChoice.English -> R.string.settings_language_english
                            LanguageChoice.Dutch -> R.string.settings_language_dutch
                        },
                    )
                    Row(
                        Modifier
                            .fillMaxWidth()
                            .heightIn(min = 48.dp)
                            .selectable(
                                selected = uiState.language == choice,
                                onClick = { onLanguageSelected(choice) },
                                role = Role.RadioButton,
                            ),
                        verticalAlignment = Alignment.CenterVertically,
                    ) {
                        RadioButton(
                            selected = uiState.language == choice,
                            onClick = null,
                            colors = RadioButtonDefaults.colors(
                                selectedColor = MaterialTheme.colorScheme.primary,
                                unselectedColor = MaterialTheme.colorScheme.onSurfaceVariant,
                            ),
                        )
                        Spacer(Modifier.width(16.dp))
                        Text(text = label, style = MaterialTheme.typography.bodyLarge, color = MaterialTheme.colorScheme.onSurface)
                    }
                }
            }
            if (uiState.languageSyncPending) {
                Spacer(Modifier.height(8.dp))
                Text(
                    text = stringResource(R.string.settings_language_sync_pending),
                    style = MaterialTheme.typography.bodySmall,
                    color = ThePreyTheme.colors.caution,
                    modifier = Modifier.semantics { liveRegion = LiveRegionMode.Polite },
                )
            }
        }
    }
}

/** Keeps the display awake on the game screen; the whole row is the toggle target. */
@Composable
private fun DisplaySection(keepScreenOn: Boolean, onKeepScreenOnChange: (Boolean) -> Unit) {
    Column(verticalArrangement = Arrangement.spacedBy(12.dp)) {
        SectionTitle(stringResource(R.string.settings_section_display))
        SectionCard {
            Row(
                Modifier
                    .fillMaxWidth()
                    .heightIn(min = 48.dp)
                    .toggleable(value = keepScreenOn, onValueChange = onKeepScreenOnChange, role = Role.Switch),
                verticalAlignment = Alignment.CenterVertically,
            ) {
                Text(
                    text = stringResource(R.string.settings_keep_screen_on),
                    style = MaterialTheme.typography.bodyLarge,
                    color = MaterialTheme.colorScheme.onSurface,
                    modifier = Modifier.weight(1f),
                )
                Spacer(Modifier.width(16.dp))
                Switch(
                    checked = keepScreenOn,
                    onCheckedChange = null,
                    colors = SwitchDefaults.colors(
                        checkedThumbColor = MaterialTheme.colorScheme.onPrimary,
                        checkedTrackColor = MaterialTheme.colorScheme.primary,
                        uncheckedThumbColor = MaterialTheme.colorScheme.onSurfaceVariant,
                        uncheckedTrackColor = MaterialTheme.colorScheme.surfaceContainerLowest,
                        uncheckedBorderColor = MaterialTheme.colorScheme.outline,
                    ),
                )
            }
            Spacer(Modifier.height(8.dp))
            Text(
                text = stringResource(R.string.settings_keep_screen_on_body),
                style = MaterialTheme.typography.bodySmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
        }
    }
}

/** Brings the one-time tours back: they show again the next time their screen comes up. */
@Composable
private fun HintsSection(onResetHints: () -> Unit) {
    Column(verticalArrangement = Arrangement.spacedBy(12.dp)) {
        SectionTitle(stringResource(R.string.settings_section_hints))
        Text(
            text = stringResource(R.string.settings_hints_body),
            style = MaterialTheme.typography.bodyMedium,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )
        SecondaryButton(text = stringResource(R.string.settings_hints_reset), onClick = onResetHints, height = 48.dp)
    }
}

@Composable
private fun LocationSection(location: LocationSectionState, onPrivacyPolicy: () -> Unit, onGrant: () -> Unit) {
    Column(verticalArrangement = Arrangement.spacedBy(12.dp)) {
        SectionTitle(stringResource(R.string.settings_section_location))
        SectionCard {
            val (accessText, accessColor) = when (location.access) {
                LocationAccess.Missing -> R.string.settings_location_off to MaterialTheme.colorScheme.error
                LocationAccess.ApproximateOnly -> R.string.settings_location_approximate to MaterialTheme.colorScheme.error
                LocationAccess.ForegroundOnly -> R.string.settings_location_while_in_use to ThePreyTheme.colors.caution
                LocationAccess.Granted -> R.string.settings_location_all_the_time to MaterialTheme.colorScheme.primary
            }
            StatusRow(stringResource(R.string.settings_location_access), stringResource(accessText), accessColor)
            location.notificationsAllowed?.let { allowed ->
                StatusRow(
                    label = stringResource(R.string.settings_notifications),
                    value = stringResource(if (allowed) R.string.settings_notifications_allowed else R.string.settings_notifications_off),
                    color = if (allowed) MaterialTheme.colorScheme.primary else ThePreyTheme.colors.caution,
                )
            }
            if (location.access == LocationAccess.ApproximateOnly) {
                Spacer(Modifier.height(8.dp))
                Text(
                    text = stringResource(R.string.settings_location_approximate_explanation),
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )
            }
            if (location.access == LocationAccess.Granted) {
                Spacer(Modifier.height(8.dp))
                Text(
                    text = stringResource(R.string.settings_location_granted),
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )
            }
        }
        val instruction = when (location.step) {
            GrantStep.OpenSettingsForPrecise -> R.string.settings_location_instruction_precise
            GrantStep.OpenSettingsForAllTheTime -> R.string.settings_location_instruction_all_the_time
            GrantStep.RequestPrecise, GrantStep.None -> null
        }
        if (instruction != null) {
            Text(
                text = stringResource(instruction),
                style = MaterialTheme.typography.bodyMedium,
                color = MaterialTheme.colorScheme.onSurface,
            )
        }
        if (location.step != GrantStep.None) {
            SecondaryButton(text = stringResource(R.string.settings_location_grant), onClick = onGrant, height = 48.dp)
        }
        // Always visible, whatever the permission state.
        DisclosurePanel(onPrivacyPolicy)
    }
}

@Composable
private fun StatusRow(label: String, value: String, color: Color) {
    Row(
        Modifier.fillMaxWidth().heightIn(min = 40.dp),
        verticalAlignment = Alignment.CenterVertically,
        horizontalArrangement = Arrangement.SpaceBetween,
    ) {
        Text(text = label, style = MaterialTheme.typography.bodyMedium, color = MaterialTheme.colorScheme.onSurfaceVariant)
        Spacer(Modifier.width(16.dp))
        Text(text = value.uppercase(), style = MaterialTheme.typography.labelMedium, color = color)
    }
}

/** The design system's warning alert: Caution tint and border, alert icon, Special Elite title. */
@Composable
private fun DisclosurePanel(onPrivacyPolicy: () -> Unit) {
    val caution = ThePreyTheme.colors.caution
    Column(
        Modifier
            .fillMaxWidth()
            .background(caution.copy(alpha = 0.12f), PanelShape)
            .border(1.dp, caution.copy(alpha = 0.4f), PanelShape)
            .padding(16.dp),
    ) {
        Row(verticalAlignment = Alignment.CenterVertically) {
            Icon(
                painter = painterResource(R.drawable.ic_alert),
                contentDescription = null,
                tint = caution,
                modifier = Modifier.size(24.dp),
            )
            Spacer(Modifier.width(12.dp))
            Column {
                Text(
                    text = stringResource(R.string.location_consent_eyebrow).uppercase(),
                    style = MaterialTheme.typography.labelSmall.copy(letterSpacing = 3.sp),
                    color = caution,
                )
                Text(
                    text = stringResource(R.string.location_consent_title),
                    style = MaterialTheme.typography.titleMedium,
                    color = MaterialTheme.colorScheme.onSurface,
                )
            }
        }
        Spacer(Modifier.height(12.dp))
        LocationDisclosure(
            onPrivacyPolicy = onPrivacyPolicy,
            textColor = MaterialTheme.colorScheme.onSurface.copy(alpha = 0.8f),
            bulletColor = caution,
        )
    }
}

// --- previews ---

private val previewState = SettingsUiState(
    callsign = "NightOwl",
    currentCallsign = "NightOwl",
    displayName = "Nina Hawk",
)

@Composable
private fun SettingsPreview(uiState: SettingsUiState, location: LocationSectionState) {
    ThePreyTheme {
        SettingsContent(
            uiState = uiState,
            location = location,
            onBack = {},
            onCallsignChange = {},
            onSave = {},
            onLanguageSelected = {},
            onPrivacyPolicy = {},
            onGrant = {},
        )
    }
}

private val offLocation = LocationSectionState(LocationAccess.Missing, notificationsAllowed = false, step = GrantStep.RequestPrecise)

@Preview(name = "Default", widthDp = 360, heightDp = 1600)
@Composable
private fun SettingsDefaultPreview() {
    SettingsPreview(previewState, offLocation)
}

@Preview(name = "Default (nl)", locale = "nl", widthDp = 360, heightDp = 1600)
@Composable
private fun SettingsDefaultDutchPreview() {
    SettingsPreview(previewState.copy(language = LanguageChoice.Dutch), offLocation)
}

@Preview(name = "Keep screen on", widthDp = 360, heightDp = 1600)
@Composable
private fun SettingsKeepScreenOnPreview() {
    SettingsPreview(previewState.copy(keepScreenOn = true), offLocation)
}

@Preview(name = "Keep screen on (nl)", locale = "nl", widthDp = 360, heightDp = 1600)
@Composable
private fun SettingsKeepScreenOnDutchPreview() {
    SettingsPreview(previewState.copy(language = LanguageChoice.Dutch, keepScreenOn = true), offLocation)
}

@Preview(name = "Saving", widthDp = 360, heightDp = 1600)
@Composable
private fun SettingsSavingPreview() {
    SettingsPreview(
        previewState.copy(callsign = "Night Owl #7", check = CallsignCheck.Valid("Night Owl #7"), saving = true),
        offLocation,
    )
}

@Preview(name = "Server error", widthDp = 360, heightDp = 1600)
@Composable
private fun SettingsServerErrorPreview() {
    SettingsPreview(
        previewState.copy(callsign = "Night Owl", check = CallsignCheck.Valid("Night Owl"), saveStatus = CallsignSaveStatus.Failed),
        offLocation,
    )
}

@Preview(name = "Approximate only", widthDp = 360, heightDp = 1600)
@Composable
private fun SettingsApproximatePreview() {
    SettingsPreview(
        previewState.copy(currentCallsign = "", callsign = "", languageSyncPending = true),
        LocationSectionState(LocationAccess.ApproximateOnly, notificationsAllowed = true, step = GrantStep.OpenSettingsForPrecise),
    )
}

@Preview(name = "While in use", widthDp = 360, heightDp = 1600)
@Composable
private fun SettingsWhileInUsePreview() {
    SettingsPreview(
        previewState,
        LocationSectionState(LocationAccess.ForegroundOnly, notificationsAllowed = true, step = GrantStep.OpenSettingsForAllTheTime),
    )
}

@Preview(name = "All the time", widthDp = 360, heightDp = 1600)
@Composable
private fun SettingsAllTheTimePreview() {
    SettingsPreview(
        previewState.copy(saveStatus = CallsignSaveStatus.Saved),
        LocationSectionState(LocationAccess.Granted, notificationsAllowed = true, step = GrantStep.None),
    )
}
