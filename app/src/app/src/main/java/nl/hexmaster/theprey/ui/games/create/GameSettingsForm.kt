package nl.hexmaster.theprey.ui.games.create

import androidx.annotation.StringRes
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.SegmentedButton
import androidx.compose.material3.SegmentedButtonDefaults
import androidx.compose.material3.SingleChoiceSegmentedButtonRow
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.res.pluralStringResource
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.tooling.preview.Preview
import androidx.compose.ui.unit.dp
import nl.hexmaster.theprey.R
import nl.hexmaster.theprey.ui.theme.ThePreyTheme

/** Label and one-line help of each setting. */
internal val GameSetting.labelRes: Int
    @StringRes get() = when (this) {
        GameSetting.Duration -> R.string.game_create_duration
        GameSetting.HunterDelay -> R.string.game_create_hunter_delay
        GameSetting.FinalStage -> R.string.game_create_final_stage
        GameSetting.PingInterval -> R.string.game_create_ping_interval
        GameSetting.FinalPingInterval -> R.string.game_create_final_ping_interval
    }

internal val GameSetting.helpRes: Int
    @StringRes get() = when (this) {
        GameSetting.Duration -> R.string.game_create_duration_help
        GameSetting.HunterDelay -> R.string.game_create_hunter_delay_help
        GameSetting.FinalStage -> R.string.game_create_final_stage_help
        GameSetting.PingInterval -> R.string.game_create_ping_interval_help
        GameSetting.FinalPingInterval -> R.string.game_create_final_ping_interval_help
    }

/**
 * The five settings as labelled segmented choices. Options the rules do not allow are disabled;
 * with [enabled] false (a request in flight) every option is.
 */
@Composable
fun GameSettingsForm(
    settings: GameSettings,
    onChange: (GameSetting, Int) -> Unit,
    modifier: Modifier = Modifier,
    enabled: Boolean = true,
) {
    Column(modifier, verticalArrangement = Arrangement.spacedBy(24.dp)) {
        GameSetting.entries.forEach { setting ->
            SettingChoice(setting, settings, onChange, enabled)
        }
    }
}

@Composable
private fun SettingChoice(
    setting: GameSetting,
    settings: GameSettings,
    onChange: (GameSetting, Int) -> Unit,
    enabled: Boolean,
) {
    Column {
        Text(
            text = stringResource(setting.labelRes).uppercase(),
            style = MaterialTheme.typography.labelMedium,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )
        Spacer(Modifier.height(4.dp))
        Text(
            text = stringResource(setting.helpRes),
            style = MaterialTheme.typography.bodySmall,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )
        Spacer(Modifier.height(8.dp))
        // Segmented control on Void; the selected segment is raised with Signal text.
        val colors = SegmentedButtonDefaults.colors(
            activeContainerColor = MaterialTheme.colorScheme.surfaceVariant,
            activeContentColor = MaterialTheme.colorScheme.primary,
            activeBorderColor = MaterialTheme.colorScheme.outline,
            inactiveContainerColor = MaterialTheme.colorScheme.surfaceContainerLowest,
            inactiveContentColor = MaterialTheme.colorScheme.onSurface,
            inactiveBorderColor = MaterialTheme.colorScheme.outline,
            disabledActiveContainerColor = MaterialTheme.colorScheme.surfaceVariant,
            disabledActiveContentColor = ThePreyTheme.colors.textGhost,
            disabledActiveBorderColor = MaterialTheme.colorScheme.outline,
            disabledInactiveContainerColor = MaterialTheme.colorScheme.surfaceContainerLowest,
            disabledInactiveContentColor = ThePreyTheme.colors.textGhost,
            disabledInactiveBorderColor = MaterialTheme.colorScheme.outline,
        )
        SingleChoiceSegmentedButtonRow(Modifier.fillMaxWidth()) {
            setting.options.forEachIndexed { index, minutes ->
                SegmentedButton(
                    selected = settings.valueOf(setting) == minutes,
                    onClick = { onChange(setting, minutes) },
                    enabled = enabled && settings.isEnabled(setting, minutes),
                    shape = SegmentedButtonDefaults.itemShape(index, setting.options.size, MaterialTheme.shapes.small),
                    colors = colors,
                    icon = {},
                    label = {
                        Text(
                            text = pluralStringResource(R.plurals.game_create_minutes, minutes, minutes),
                            style = MaterialTheme.typography.labelLarge,
                        )
                    },
                )
            }
        }
    }
}

/** The five settings read-only, for players who may not change them. */
@Composable
fun GameSettingsSummary(settings: GameSettings, modifier: Modifier = Modifier) {
    Column(modifier, verticalArrangement = Arrangement.spacedBy(12.dp)) {
        GameSetting.entries.forEach { setting ->
            androidx.compose.foundation.layout.Row(
                Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween,
            ) {
                Text(
                    text = stringResource(setting.labelRes).uppercase(),
                    style = MaterialTheme.typography.labelMedium,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                    modifier = Modifier.weight(1f),
                )
                val minutes = settings.valueOf(setting)
                Text(
                    text = pluralStringResource(R.plurals.game_create_minutes, minutes, minutes),
                    style = MaterialTheme.typography.titleMedium,
                    color = MaterialTheme.colorScheme.onSurface,
                )
            }
        }
    }
}

@Preview(name = "Settings form")
@Composable
private fun GameSettingsFormPreview() {
    ThePreyTheme {
        Column(
            Modifier.background(MaterialTheme.colorScheme.background).padding(16.dp),
            verticalArrangement = Arrangement.spacedBy(32.dp),
        ) {
            GameSettingsForm(settings = GameSettings(pingInterval = 2), onChange = { _, _ -> })
            GameSettingsSummary(settings = GameSettings())
        }
    }
}

@Preview(name = "Settings form (Dutch, locked)", locale = "nl")
@Composable
private fun GameSettingsFormDutchPreview() {
    ThePreyTheme {
        GameSettingsForm(
            settings = GameSettings(),
            onChange = { _, _ -> },
            enabled = false,
            modifier = Modifier.background(Color.Transparent).padding(16.dp),
        )
    }
}
