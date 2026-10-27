package nl.hexmaster.theprey.ui.consent

import android.content.res.Configuration
import androidx.annotation.StringRes
import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.WindowInsets
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.safeDrawing
import androidx.compose.foundation.layout.widthIn
import androidx.compose.foundation.layout.windowInsetsPadding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.tooling.preview.Preview
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.compose.ui.window.Dialog
import androidx.compose.ui.window.DialogProperties
import nl.hexmaster.theprey.R
import nl.hexmaster.theprey.ui.components.PrimaryButton
import nl.hexmaster.theprey.ui.components.SecondaryButton
import nl.hexmaster.theprey.ui.theme.ThePreyTheme

/** Which text and primary action the disclosure shows. */
enum class DisclosureVariant {
    /** First ask: Continue opens the system location prompt. */
    Disclosure,

    /** The system no longer prompts: the primary action opens the app's settings page. */
    OpenSettings,

    /** Precise location was granted "While using the app": the primary action opens settings for "Allow all the time". */
    AllTheTime,
}

/** `r-lg` from the design system: sheets and dialogs. */
private val DialogShape = RoundedCornerShape(20.dp)

/**
 * The prominent location tracking disclosure. Non-dismissible: Back and outside taps do nothing,
 * it closes only through [onPrimary] (Continue / Open settings) or [onNotNow].
 */
@Composable
fun LocationDisclosureDialog(
    variant: DisclosureVariant,
    onPrimary: () -> Unit,
    onNotNow: () -> Unit,
    onPrivacyPolicy: () -> Unit,
) {
    Dialog(
        onDismissRequest = {},
        properties = DialogProperties(
            dismissOnBackPress = false,
            dismissOnClickOutside = false,
            usePlatformDefaultWidth = false,
        ),
    ) {
        LocationDisclosureContent(variant, onPrimary, onNotNow, onPrivacyPolicy)
    }
}

@Composable
fun LocationDisclosureContent(
    variant: DisclosureVariant,
    onPrimary: () -> Unit,
    onNotNow: () -> Unit,
    onPrivacyPolicy: () -> Unit,
    modifier: Modifier = Modifier,
) {
    Box(
        modifier = modifier
            .fillMaxSize()
            .windowInsetsPadding(WindowInsets.safeDrawing)
            .padding(16.dp),
        contentAlignment = Alignment.Center,
    ) {
        Surface(
            modifier = Modifier.widthIn(max = 480.dp).fillMaxWidth(),
            shape = DialogShape,
            color = MaterialTheme.colorScheme.surface,
            contentColor = MaterialTheme.colorScheme.onSurface,
            border = BorderStroke(1.dp, MaterialTheme.colorScheme.outline),
            shadowElevation = 24.dp,
        ) {
            Column(
                modifier = Modifier
                    .verticalScroll(rememberScrollState())
                    .padding(24.dp),
            ) {
                val settings = variant != DisclosureVariant.Disclosure
                Text(
                    text = stringResource(R.string.location_consent_eyebrow).uppercase(),
                    style = MaterialTheme.typography.labelSmall.copy(letterSpacing = 3.sp),
                    color = MaterialTheme.colorScheme.secondary,
                )
                Spacer(Modifier.height(12.dp))
                Text(
                    text = stringResource(
                        when (variant) {
                            DisclosureVariant.Disclosure -> R.string.location_consent_title
                            DisclosureVariant.OpenSettings -> R.string.location_consent_settings_title
                            DisclosureVariant.AllTheTime -> R.string.location_consent_all_the_time_title
                        },
                    ),
                    style = MaterialTheme.typography.headlineSmall,
                    color = MaterialTheme.colorScheme.onSurface,
                )
                Spacer(Modifier.height(16.dp))
                if (variant == DisclosureVariant.AllTheTime) {
                    // The player just accepted the full disclosure; this step only says where to go.
                    BodyText(R.string.location_consent_all_the_time_body, color = MaterialTheme.colorScheme.onSurface)
                    Spacer(Modifier.height(12.dp))
                    BodyText(R.string.location_consent_all_the_time_steps)
                } else {
                    if (settings) {
                        BodyText(R.string.location_consent_settings_body, color = MaterialTheme.colorScheme.onSurface)
                        Spacer(Modifier.height(16.dp))
                    }
                    // The full disclosure is part of both variants: settings changes the permission too.
                    LocationDisclosure(onPrivacyPolicy = onPrivacyPolicy)
                }
                Spacer(Modifier.height(16.dp))
                PrimaryButton(
                    text = stringResource(
                        if (settings) R.string.location_consent_settings_open else R.string.location_consent_continue,
                    ),
                    onClick = onPrimary,
                )
                Spacer(Modifier.height(12.dp))
                SecondaryButton(
                    text = stringResource(R.string.location_consent_not_now),
                    onClick = onNotNow,
                    height = 48.dp,
                )
            }
        }
    }
}

@Composable
private fun BodyText(@StringRes text: Int, color: Color = MaterialTheme.colorScheme.onSurfaceVariant) {
    Text(text = stringResource(text), style = MaterialTheme.typography.bodyMedium, color = color)
}

@Preview(widthDp = 360, heightDp = 800)
@Composable
private fun LocationDisclosurePreview() {
    ThePreyTheme {
        Box(Modifier.background(MaterialTheme.colorScheme.background)) {
            LocationDisclosureContent(DisclosureVariant.Disclosure, onPrimary = {}, onNotNow = {}, onPrivacyPolicy = {})
        }
    }
}

@Preview(widthDp = 360, heightDp = 800)
@Composable
private fun LocationDisclosureSettingsPreview() {
    ThePreyTheme {
        Box(Modifier.background(MaterialTheme.colorScheme.background)) {
            LocationDisclosureContent(DisclosureVariant.OpenSettings, onPrimary = {}, onNotNow = {}, onPrivacyPolicy = {})
        }
    }
}

@Preview(widthDp = 360, heightDp = 800)
@Preview(widthDp = 360, heightDp = 800, uiMode = Configuration.UI_MODE_NIGHT_YES)
@Composable
private fun LocationDisclosureAllTheTimePreview() {
    ThePreyTheme {
        Box(Modifier.background(MaterialTheme.colorScheme.background)) {
            LocationDisclosureContent(DisclosureVariant.AllTheTime, onPrimary = {}, onNotNow = {}, onPrivacyPolicy = {})
        }
    }
}
