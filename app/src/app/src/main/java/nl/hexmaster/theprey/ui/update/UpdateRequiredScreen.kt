package nl.hexmaster.theprey.ui.update

import androidx.activity.compose.BackHandler
import androidx.activity.compose.LocalActivity
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.safeDrawingPadding
import androidx.compose.foundation.layout.widthIn
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.semantics.heading
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.tooling.preview.Preview
import androidx.compose.ui.unit.dp
import nl.hexmaster.theprey.R
import nl.hexmaster.theprey.ui.components.PreyMark
import nl.hexmaster.theprey.ui.components.PrimaryButton
import nl.hexmaster.theprey.ui.theme.ThePreyTheme

/**
 * Shown over every screen while this app version is no longer supported. It cannot be dismissed:
 * it takes every touch, Back leaves the app, and the only action opens the Play Store listing.
 */
@Composable
fun UpdateRequiredScreen(modifier: Modifier = Modifier) {
    val activity = LocalActivity.current
    val context = LocalContext.current
    BackHandler { activity?.finish() }

    UpdateRequiredContent(onUpdate = { openPlayStore(context) }, modifier = modifier)
}

@Composable
private fun UpdateRequiredContent(onUpdate: () -> Unit, modifier: Modifier = Modifier) {
    Box(
        modifier
            .fillMaxSize()
            .background(MaterialTheme.colorScheme.background)
            // Swallow every touch so nothing underneath can be reached.
            .clickable(interactionSource = null, indication = null, onClick = {})
            .safeDrawingPadding(),
        contentAlignment = Alignment.Center,
    ) {
        Column(
            horizontalAlignment = Alignment.CenterHorizontally,
            modifier = Modifier
                .widthIn(max = 420.dp)
                .verticalScroll(rememberScrollState())
                .padding(24.dp),
        ) {
            PreyMark(markSize = 96.dp, contentDescription = null)
            Spacer(Modifier.height(32.dp))
            Text(
                text = stringResource(R.string.update_required_title),
                style = MaterialTheme.typography.headlineSmall,
                color = MaterialTheme.colorScheme.onBackground,
                textAlign = TextAlign.Center,
                modifier = Modifier.semantics { heading() },
            )
            Spacer(Modifier.height(12.dp))
            Text(
                text = stringResource(R.string.update_required_body),
                style = MaterialTheme.typography.bodyMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
                textAlign = TextAlign.Center,
            )
            Spacer(Modifier.height(32.dp))
            PrimaryButton(
                text = stringResource(R.string.update_required_action),
                onClick = onUpdate,
                modifier = Modifier.fillMaxWidth(),
            )
        }
    }
}

// ---- Previews ----

@Preview(name = "Update required", showSystemUi = true)
@Composable
private fun UpdateRequiredPreview() {
    ThePreyTheme { UpdateRequiredContent(onUpdate = {}) }
}

@Preview(name = "Update required (Dutch)", showSystemUi = true, locale = "nl")
@Composable
private fun UpdateRequiredDutchPreview() {
    ThePreyTheme { UpdateRequiredContent(onUpdate = {}) }
}
