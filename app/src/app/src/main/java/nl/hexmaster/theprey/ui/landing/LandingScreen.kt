package nl.hexmaster.theprey.ui.landing

import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.Image
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.SpanStyle
import androidx.compose.ui.text.buildAnnotatedString
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.text.withStyle
import androidx.compose.ui.tooling.preview.Preview
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import nl.hexmaster.theprey.R
import nl.hexmaster.theprey.auth.SignInError
import nl.hexmaster.theprey.ui.components.PrimaryButton
import nl.hexmaster.theprey.ui.theme.ThePreyTheme

@Composable
fun LandingScreen(viewModel: LandingViewModel, modifier: Modifier = Modifier) {
    val uiState by viewModel.uiState.collectAsStateWithLifecycle()
    val context = LocalContext.current
    LandingContent(uiState = uiState, onSignIn = { viewModel.signIn(context) }, modifier = modifier)
}

@Composable
fun LandingContent(uiState: LandingUiState, onSignIn: () -> Unit, modifier: Modifier = Modifier) {
    Scaffold(modifier = modifier.fillMaxSize()) { innerPadding ->
        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(innerPadding)
                .padding(horizontal = 16.dp, vertical = 24.dp),
            horizontalAlignment = Alignment.CenterHorizontally,
        ) {
            Column(
                modifier = Modifier.weight(1f).fillMaxWidth(),
                verticalArrangement = Arrangement.Center,
                horizontalAlignment = Alignment.CenterHorizontally,
            ) {
                Image(
                    painter = painterResource(R.drawable.ic_prey_mark),
                    contentDescription = stringResource(R.string.landing_mark_description),
                    modifier = Modifier.size(160.dp),
                )
                Spacer(Modifier.height(32.dp))
                BrandTitle()
                Spacer(Modifier.height(12.dp))
                Text(
                    text = stringResource(R.string.landing_tagline).uppercase(),
                    style = MaterialTheme.typography.labelLarge.copy(letterSpacing = 3.sp),
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                    textAlign = TextAlign.Center,
                )
            }

            val message = uiState.error?.let { stringResource(it.messageRes()) }
                ?: if (uiState.sessionExpired) stringResource(R.string.landing_session_expired) else null
            if (message != null) {
                StatusMessage(text = message, isError = uiState.error != null)
                Spacer(Modifier.height(16.dp))
            } else {
                Text(
                    text = stringResource(R.string.landing_intro),
                    style = MaterialTheme.typography.bodyMedium,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                    textAlign = TextAlign.Center,
                )
                Spacer(Modifier.height(16.dp))
            }

            PrimaryButton(
                text = stringResource(R.string.landing_sign_in),
                busyText = stringResource(R.string.landing_signing_in),
                busy = uiState.isBusy,
                onClick = onSignIn,
            )
        }
    }
}

/** "THE PREY" lockup: "PREY" in Signal green. */
@Composable
private fun BrandTitle() {
    val the = stringResource(R.string.brand_word_the).uppercase()
    val prey = stringResource(R.string.brand_word_prey).uppercase()
    val signal = ThePreyTheme.colors.signal
    Text(
        text = buildAnnotatedString {
            append("$the ")
            withStyle(SpanStyle(color = signal)) { append(prey) }
        },
        style = MaterialTheme.typography.displayMedium,
        color = MaterialTheme.colorScheme.onBackground,
    )
}

/** Inline alert: danger-tinted for errors, info-tinted for neutral notices. */
@Composable
private fun StatusMessage(text: String, isError: Boolean) {
    val accent = if (isError) ThePreyTheme.colors.hunter else ThePreyTheme.colors.info
    Surface(
        modifier = Modifier.fillMaxWidth(),
        shape = MaterialTheme.shapes.medium,
        color = accent.copy(alpha = 0.12f),
        border = BorderStroke(1.dp, accent.copy(alpha = 0.4f)),
    ) {
        Text(
            text = text,
            modifier = Modifier.padding(16.dp),
            style = MaterialTheme.typography.bodyMedium,
            color = MaterialTheme.colorScheme.onSurface,
        )
    }
}

private fun SignInError.messageRes(): Int = when (this) {
    SignInError.Network -> R.string.error_sign_in_network
    SignInError.NoEmail -> R.string.error_sign_in_no_email
    SignInError.Generic -> R.string.error_sign_in_generic
}

@Preview
@Composable
private fun LandingIdlePreview() {
    ThePreyTheme { LandingContent(uiState = LandingUiState(), onSignIn = {}) }
}

@Preview
@Composable
private fun LandingBusyPreview() {
    ThePreyTheme { LandingContent(uiState = LandingUiState(isBusy = true), onSignIn = {}) }
}

@Preview
@Composable
private fun LandingErrorPreview() {
    ThePreyTheme { LandingContent(uiState = LandingUiState(error = SignInError.Network), onSignIn = {}) }
}
