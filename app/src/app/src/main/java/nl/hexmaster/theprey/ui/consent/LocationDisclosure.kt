package nl.hexmaster.theprey.ui.consent

import androidx.annotation.StringRes
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.ReadOnlyComposable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.LocalConfiguration
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.style.TextDecoration
import androidx.compose.ui.tooling.preview.Preview
import androidx.compose.ui.unit.dp
import nl.hexmaster.theprey.R
import nl.hexmaster.theprey.ui.theme.ThePreyTheme

private const val PRIVACY_URL_EN = "https://theprey.nl/en/privacy/"
private const val PRIVACY_URL_NL = "https://theprey.nl/nl/privacy/"

/** The privacy page on theprey.nl in [language] (`nl` or anything else for English). */
fun privacyPolicyUrl(language: String): String = if (language == "nl") PRIVACY_URL_NL else PRIVACY_URL_EN

/** The privacy page in the app's current display language. */
@Composable
@ReadOnlyComposable
fun currentPrivacyPolicyUrl(): String = privacyPolicyUrl(LocalConfiguration.current.locales[0].language)

/**
 * The location tracking disclosure statements and the privacy link, the one copy shared by the
 * consent dialog and the Settings screen's warning panel, so the two can never drift apart. Keep
 * the strings in line with the privacy page and docs/play-store/location-disclosure.md.
 */
@Composable
fun LocationDisclosure(
    onPrivacyPolicy: () -> Unit,
    modifier: Modifier = Modifier,
    textColor: Color = MaterialTheme.colorScheme.onSurfaceVariant,
    bulletColor: Color = MaterialTheme.colorScheme.secondary,
) {
    Column(modifier) {
        DisclosureText(R.string.location_consent_body, textColor)
        Spacer(Modifier.height(12.dp))
        Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
            DisclosurePoint(R.string.location_consent_point_background, textColor, bulletColor)
            DisclosurePoint(R.string.location_consent_point_notification, textColor, bulletColor)
            DisclosurePoint(R.string.location_consent_point_stop, textColor, bulletColor)
            DisclosurePoint(R.string.location_consent_point_purpose, textColor, bulletColor)
            DisclosurePoint(R.string.location_consent_point_near_me, textColor, bulletColor)
        }
        Spacer(Modifier.height(8.dp))
        TextButton(onClick = onPrivacyPolicy, modifier = Modifier.height(48.dp)) {
            Text(
                text = stringResource(R.string.location_consent_privacy),
                style = MaterialTheme.typography.bodyMedium.copy(textDecoration = TextDecoration.Underline),
                color = MaterialTheme.colorScheme.tertiary,
            )
        }
    }
}

@Composable
private fun DisclosureText(@StringRes text: Int, color: Color) {
    Text(text = stringResource(text), style = MaterialTheme.typography.bodyMedium, color = color)
}

@Composable
private fun DisclosurePoint(@StringRes text: Int, color: Color, bulletColor: Color) {
    Row(verticalAlignment = Alignment.Top) {
        Box(
            Modifier
                .padding(top = 7.dp, end = 12.dp)
                .size(6.dp)
                .background(bulletColor),
        )
        DisclosureText(text, color)
    }
}

@Preview(name = "English", locale = "en", widthDp = 360)
@Preview(name = "Nederlands", locale = "nl", widthDp = 360)
@Composable
private fun LocationDisclosurePreview() {
    ThePreyTheme {
        Box(Modifier.background(MaterialTheme.colorScheme.surface).padding(16.dp)) {
            LocationDisclosure(onPrivacyPolicy = {})
        }
    }
}
