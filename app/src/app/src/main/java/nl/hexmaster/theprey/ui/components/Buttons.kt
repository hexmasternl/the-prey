package nl.hexmaster.theprey.ui.components

import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.LocalContentColor
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.TextStyle
import androidx.compose.ui.tooling.preview.Preview
import androidx.compose.ui.unit.Dp
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import nl.hexmaster.theprey.ui.theme.BodyFontFamily
import nl.hexmaster.theprey.ui.theme.ThePreyTheme

private val ButtonHeight = 56.dp

private val buttonTextStyle = TextStyle(fontFamily = BodyFontFamily, fontSize = 14.sp, letterSpacing = 1.2.sp)

/** Fill of a [PrimaryButton]: Signal for the normal primary action, Hunter for a red (danger/hunt) action. */
enum class PrimaryButtonStyle { Signal, Hunter }

/**
 * The one primary action of a screen: Signal (or Hunter) fill, Void label. Shows a spinner and
 * [busyText] while [busy]. Disabled looks the same for every [style].
 */
@Composable
fun PrimaryButton(
    text: String,
    onClick: () -> Unit,
    modifier: Modifier = Modifier,
    busy: Boolean = false,
    busyText: String = text,
    enabled: Boolean = true,
    height: Dp = ButtonHeight,
    style: PrimaryButtonStyle = PrimaryButtonStyle.Signal,
) {
    Button(
        onClick = onClick,
        enabled = enabled && !busy,
        modifier = modifier.fillMaxWidth().height(height),
        shape = MaterialTheme.shapes.small,
        colors = ButtonDefaults.buttonColors(
            containerColor = when (style) {
                PrimaryButtonStyle.Signal -> MaterialTheme.colorScheme.primary
                PrimaryButtonStyle.Hunter -> ThePreyTheme.colors.hunter
            },
            contentColor = when (style) {
                PrimaryButtonStyle.Signal -> MaterialTheme.colorScheme.onPrimary
                PrimaryButtonStyle.Hunter -> MaterialTheme.colorScheme.onError
            },
            disabledContainerColor = MaterialTheme.colorScheme.surfaceVariant,
            disabledContentColor = MaterialTheme.colorScheme.onSurfaceVariant,
        ),
    ) {
        ButtonLabel(text = if (busy) busyText else text, busy = busy)
    }
}

/** A secondary action: transparent with Signal text and a Signal-Dim border. */
@Composable
fun SecondaryButton(
    text: String,
    onClick: () -> Unit,
    modifier: Modifier = Modifier,
    busy: Boolean = false,
    enabled: Boolean = true,
    height: Dp = ButtonHeight,
) {
    OutlinedButton(
        onClick = onClick,
        enabled = enabled && !busy,
        modifier = modifier.fillMaxWidth().height(height),
        shape = MaterialTheme.shapes.small,
        border = BorderStroke(1.dp, MaterialTheme.colorScheme.secondary),
        colors = ButtonDefaults.outlinedButtonColors(
            contentColor = MaterialTheme.colorScheme.primary,
            disabledContentColor = ThePreyTheme.colors.textGhost,
        ),
    ) {
        ButtonLabel(text = text, busy = busy)
    }
}

@Composable
private fun ButtonLabel(text: String, busy: Boolean) {
    Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.Center) {
        if (busy) {
            CircularProgressIndicator(
                modifier = Modifier.size(18.dp),
                color = LocalContentColor.current,
                strokeWidth = 2.dp,
            )
            Spacer(Modifier.width(12.dp))
        }
        Text(text = text.uppercase(), style = buttonTextStyle)
    }
}

@Preview
@Composable
private fun PrimaryButtonStylesPreview() {
    ThePreyTheme {
        Column(
            Modifier.background(MaterialTheme.colorScheme.background).padding(16.dp),
            verticalArrangement = Arrangement.spacedBy(12.dp),
        ) {
            PrimaryButton(text = "Play now", onClick = {})
            PrimaryButton(text = "Play now", onClick = {}, enabled = false)
            PrimaryButton(text = "Resume game", onClick = {}, style = PrimaryButtonStyle.Hunter)
            PrimaryButton(text = "Resume game", onClick = {}, style = PrimaryButtonStyle.Hunter, enabled = false)
        }
    }
}
