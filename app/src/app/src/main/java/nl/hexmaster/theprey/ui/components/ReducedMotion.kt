package nl.hexmaster.theprey.ui.components

import android.provider.Settings
import androidx.compose.runtime.Composable
import androidx.compose.runtime.remember
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.platform.LocalInspectionMode

/**
 * True when the player turned animations off (animator duration scale 0). Looping, decorative
 * animations must then stay still; state must still be readable without them.
 */
@Composable
fun rememberReducedMotion(): Boolean {
    val context = LocalContext.current
    if (LocalInspectionMode.current) return false
    return remember(context) {
        Settings.Global.getFloat(context.contentResolver, Settings.Global.ANIMATOR_DURATION_SCALE, 1f) == 0f
    }
}
