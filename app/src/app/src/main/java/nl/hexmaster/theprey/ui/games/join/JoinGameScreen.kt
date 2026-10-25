package nl.hexmaster.theprey.ui.games.join

import androidx.activity.compose.BackHandler
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.safeDrawingPadding
import androidx.compose.foundation.layout.widthIn
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.rememberUpdatedState
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.tooling.preview.Preview
import androidx.compose.ui.unit.dp
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import nl.hexmaster.theprey.R
import nl.hexmaster.theprey.ui.components.PrimaryButton
import nl.hexmaster.theprey.ui.components.SecondaryButton
import nl.hexmaster.theprey.ui.consent.LocalLocationConsent
import nl.hexmaster.theprey.ui.playfields.MessageState
import nl.hexmaster.theprey.ui.theme.ThePreyTheme

/**
 * The step between a tapped invitation and the lobby: asks for precise location first, then shows
 * "Joining game" while the view model joins, or a connection error with Retry and Cancel. Back
 * cancels. [onExit] is called once with where to go.
 */
@Composable
fun JoinGameScreen(
    viewModel: JoinGameViewModel,
    onExit: (JoinExit) -> Unit,
    modifier: Modifier = Modifier,
) {
    val ui by viewModel.uiState.collectAsStateWithLifecycle()
    val consent = LocalLocationConsent.current
    val currentOnExit by rememberUpdatedState(onExit)

    LaunchedEffect(Unit) {
        if (viewModel.uiState.value == JoinUiState.AwaitingLocation) {
            consent.requireLocation(onDeclined = viewModel::declineLocation, onGranted = viewModel::start)
        }
    }
    LaunchedEffect(ui) {
        (ui as? JoinUiState.Done)?.let { currentOnExit(it.exit) }
    }
    BackHandler { viewModel.cancel() }

    JoinGameContent(ui = ui, onRetry = viewModel::retry, onCancel = viewModel::cancel, modifier = modifier)
}

@Composable
private fun JoinGameContent(
    ui: JoinUiState,
    onRetry: () -> Unit,
    onCancel: () -> Unit,
    modifier: Modifier = Modifier,
) {
    Box(
        modifier.fillMaxSize().background(MaterialTheme.colorScheme.background).safeDrawingPadding(),
        contentAlignment = Alignment.Center,
    ) {
        when (ui) {
            JoinUiState.Error -> Column(
                horizontalAlignment = Alignment.CenterHorizontally,
                modifier = Modifier.widthIn(max = 420.dp).padding(24.dp),
            ) {
                Text(
                    text = stringResource(R.string.join_error),
                    style = MaterialTheme.typography.bodyMedium,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                    textAlign = TextAlign.Center,
                )
                Spacer(Modifier.height(20.dp))
                Row(horizontalArrangement = Arrangement.spacedBy(12.dp), modifier = Modifier.fillMaxWidth()) {
                    SecondaryButton(text = stringResource(R.string.join_cancel), onClick = onCancel, modifier = Modifier.weight(1f))
                    PrimaryButton(text = stringResource(R.string.join_retry), onClick = onRetry, modifier = Modifier.weight(1f))
                }
            }
            else -> MessageState(text = stringResource(R.string.join_progress), loading = true)
        }
    }
}

// ---- Previews ----

@Preview(name = "Joining")
@Composable
private fun JoiningPreview() {
    ThePreyTheme { JoinGameContent(ui = JoinUiState.Joining, onRetry = {}, onCancel = {}) }
}

@Preview(name = "Joining (Dutch)", locale = "nl")
@Composable
private fun JoiningDutchPreview() {
    ThePreyTheme { JoinGameContent(ui = JoinUiState.Joining, onRetry = {}, onCancel = {}) }
}

@Preview(name = "Error")
@Composable
private fun ErrorPreview() {
    ThePreyTheme { JoinGameContent(ui = JoinUiState.Error, onRetry = {}, onCancel = {}) }
}

@Preview(name = "Error (Dutch)", locale = "nl")
@Composable
private fun ErrorDutchPreview() {
    ThePreyTheme { JoinGameContent(ui = JoinUiState.Error, onRetry = {}, onCancel = {}) }
}
