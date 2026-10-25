package nl.hexmaster.theprey.ui.consent

import android.Manifest
import android.app.Activity
import android.content.ActivityNotFoundException
import android.content.Context
import android.content.Intent
import android.content.pm.PackageManager
import android.net.Uri
import android.os.Build
import android.provider.Settings
import androidx.activity.compose.LocalActivity
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.runtime.Composable
import androidx.compose.runtime.CompositionLocalProvider
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.Stable
import androidx.compose.runtime.compositionLocalOf
import androidx.compose.runtime.getValue
import androidx.compose.runtime.remember
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.platform.LocalUriHandler
import androidx.core.app.ActivityCompat
import androidx.core.content.ContextCompat
import androidx.lifecycle.Lifecycle
import androidx.lifecycle.compose.LifecycleEventEffect
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.lifecycle.viewmodel.compose.viewModel
import nl.hexmaster.theprey.data.location.LocationConsentStore

/** The location consent as seen by screens below [LocationConsentHost]. */
@Stable
interface LocationConsent {
    /** Precise location is granted: gameplay may use location. */
    val granted: Boolean

    /** Fine or coarse location is granted: enough for a position readout. */
    val canReadLocation: Boolean

    /**
     * Runs [onGranted] once precise location is granted: at once when it already is, otherwise
     * after the disclosure and a grant. Dropped when the player declines, in which case
     * [onDeclined] runs instead. Wrap every start/join game action in this.
     */
    fun requireLocation(onDeclined: () -> Unit = {}, onGranted: () -> Unit)
}

/** Outside a [LocationConsentHost] (previews): nothing granted, nothing ever runs. */
private object NoLocationConsent : LocationConsent {
    override val granted = false
    override val canReadLocation = false
    override fun requireLocation(onDeclined: () -> Unit, onGranted: () -> Unit) = Unit
}

val LocalLocationConsent = compositionLocalOf<LocationConsent> { NoLocationConsent }

private class ViewModelLocationConsent(
    override val granted: Boolean,
    override val canReadLocation: Boolean,
    private val viewModel: LocationConsentViewModel,
) : LocationConsent {
    override fun requireLocation(onDeclined: () -> Unit, onGranted: () -> Unit) =
        viewModel.requireLocation(onDeclined, onGranted)
}

private const val FINE = Manifest.permission.ACCESS_FINE_LOCATION
private const val COARSE = Manifest.permission.ACCESS_COARSE_LOCATION

/**
 * Owns the location permission flow for the signed-in area ([active]): shows the prominent
 * disclosure once by itself while [onMainMenu] and precise location is missing (once per device),
 * and again before any game action that needs location; runs the system prompts only after the
 * disclosure is accepted, and provides [LocalLocationConsent] to [content].
 */
@Composable
fun LocationConsentHost(
    store: LocationConsentStore,
    active: Boolean,
    onMainMenu: Boolean,
    content: @Composable () -> Unit,
) {
    val context = LocalContext.current
    val activity = LocalActivity.current
    val uriHandler = LocalUriHandler.current
    val privacyUrl = currentPrivacyPolicyUrl()
    val viewModel = viewModel { LocationConsentViewModel(store, Build.VERSION.SDK_INT) }
    val uiState by viewModel.uiState.collectAsStateWithLifecycle()
    val granted by viewModel.granted.collectAsStateWithLifecycle()
    val canReadLocation by viewModel.canReadLocation.collectAsStateWithLifecycle()

    val notificationLauncher = rememberLauncherForActivityResult(ActivityResultContracts.RequestPermission()) {
        // Nothing to decide: the tracking notification is shown when allowed.
    }
    val locationLauncher = rememberLauncherForActivityResult(ActivityResultContracts.RequestMultiplePermissions()) { grants ->
        viewModel.onPermissionResult(
            fineGranted = grants[FINE] == true || context.has(FINE),
            coarseGranted = grants[COARSE] == true || context.has(COARSE),
            canAskAgain = activity.canAsk(),
            notificationsGranted = context.notificationsGranted(),
        )
    }

    LaunchedEffect(active) { viewModel.onSessionChanged(active) }
    LaunchedEffect(onMainMenu) { viewModel.onMainMenuVisible(onMainMenu) }
    // Also fires on the first resume, so this covers the start check, and keeps the grant current
    // after the system settings or the Settings screen changed it.
    LifecycleEventEffect(Lifecycle.Event.ON_RESUME) {
        viewModel.onPermissionChecked(
            granted = context.has(FINE),
            canAsk = activity.canAsk(),
            approximate = context.has(COARSE),
        )
    }
    LaunchedEffect(viewModel) {
        viewModel.effects.collect { effect ->
            when (effect) {
                ConsentEffect.RequestLocation -> locationLauncher.launch(arrayOf(FINE, COARSE))
                ConsentEffect.RequestNotifications -> if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU) {
                    notificationLauncher.launch(Manifest.permission.POST_NOTIFICATIONS)
                }
                ConsentEffect.OpenAppSettings -> context.openAppSettings()
            }
        }
    }

    val consent = remember(granted, canReadLocation, viewModel) {
        ViewModelLocationConsent(granted, canReadLocation, viewModel)
    }
    CompositionLocalProvider(LocalLocationConsent provides consent) {
        content()
    }

    if (active) {
        val variant = when (uiState) {
            ConsentUiState.Hidden -> null
            ConsentUiState.Disclosure -> DisclosureVariant.Disclosure
            ConsentUiState.OpenSettings -> DisclosureVariant.OpenSettings
        }
        if (variant != null) {
            LocationDisclosureDialog(
                variant = variant,
                onPrimary = if (variant == DisclosureVariant.Disclosure) viewModel::onContinue else viewModel::onOpenSettings,
                onNotNow = viewModel::onNotNow,
                onPrivacyPolicy = { uriHandler.openUri(privacyUrl) },
            )
        }
    }
}

private fun Context.has(permission: String): Boolean =
    ContextCompat.checkSelfPermission(this, permission) == PackageManager.PERMISSION_GRANTED

private fun Context.notificationsGranted(): Boolean =
    Build.VERSION.SDK_INT < Build.VERSION_CODES.TIRAMISU || has(Manifest.permission.POST_NOTIFICATIONS)

/** Whether the system would still show the fine-location prompt after a denial. */
private fun Activity?.canAsk(): Boolean =
    this != null && ActivityCompat.shouldShowRequestPermissionRationale(this, FINE)

/** Opens The Prey's page in the system settings (falls back to the settings home). */
internal fun Context.openAppSettings() {
    val intent = Intent(Settings.ACTION_APPLICATION_DETAILS_SETTINGS, Uri.fromParts("package", packageName, null))
        .addFlags(Intent.FLAG_ACTIVITY_NEW_TASK)
    try {
        startActivity(intent)
    } catch (_: ActivityNotFoundException) {
        startActivity(Intent(Settings.ACTION_SETTINGS).addFlags(Intent.FLAG_ACTIVITY_NEW_TASK))
    }
}
