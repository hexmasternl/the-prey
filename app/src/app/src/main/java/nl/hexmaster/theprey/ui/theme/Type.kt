package nl.hexmaster.theprey.ui.theme

import androidx.compose.material3.Typography
import androidx.compose.ui.text.TextStyle
import androidx.compose.ui.text.font.Font
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.unit.sp
import nl.hexmaster.theprey.R

/** Typewriter display face: headings, titles and all numeric readouts. */
val HeadFontFamily = FontFamily(Font(R.font.special_elite))

/** Everything else: body text and (UPPERCASE, letter-spaced) labels. */
val BodyFontFamily = FontFamily(Font(R.font.pt_mono))

private val defaults = Typography()

internal val ThePreyTypography = Typography(
    displayLarge = defaults.displayLarge.copy(fontFamily = HeadFontFamily),
    displayMedium = defaults.displayMedium.copy(fontFamily = HeadFontFamily),
    displaySmall = defaults.displaySmall.copy(fontFamily = HeadFontFamily),
    headlineLarge = defaults.headlineLarge.copy(fontFamily = HeadFontFamily),
    headlineMedium = defaults.headlineMedium.copy(fontFamily = HeadFontFamily),
    headlineSmall = defaults.headlineSmall.copy(fontFamily = HeadFontFamily),
    titleLarge = defaults.titleLarge.copy(fontFamily = HeadFontFamily),
    titleMedium = defaults.titleMedium.copy(fontFamily = HeadFontFamily),
    titleSmall = defaults.titleSmall.copy(fontFamily = HeadFontFamily),
    bodyLarge = defaults.bodyLarge.copy(fontFamily = BodyFontFamily),
    bodyMedium = defaults.bodyMedium.copy(fontFamily = BodyFontFamily),
    bodySmall = defaults.bodySmall.copy(fontFamily = BodyFontFamily),
    labelLarge = labelStyle(defaults.labelLarge),
    labelMedium = labelStyle(defaults.labelMedium),
    labelSmall = labelStyle(defaults.labelSmall),
)

private fun labelStyle(base: TextStyle) = base.copy(fontFamily = BodyFontFamily, letterSpacing = 1.5.sp)
