package nl.hexmaster.theprey.ui.components

import androidx.compose.foundation.Image
import androidx.compose.foundation.layout.requiredSize
import androidx.compose.foundation.layout.size
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.unit.Dp
import nl.hexmaster.theprey.R

/**
 * `ic_prey_mark` is drawn on the 108 dp adaptive-icon grid with the mark in the central 66 dp, so
 * it can double as launcher and splash icon. Size the drawable box by this factor to get a mark of
 * a given visible size.
 */
const val PREY_MARK_BOX_RATIO = 108f / 66f

/**
 * The Prey mark at [markSize] (the visible reticle width). The transparent safe-zone padding of the
 * shared vector overflows the layout bounds, so the mark lays out like a tightly cropped image.
 */
@Composable
fun PreyMark(markSize: Dp, contentDescription: String?, modifier: Modifier = Modifier) {
    Image(
        painter = painterResource(R.drawable.ic_prey_mark),
        contentDescription = contentDescription,
        modifier = modifier.size(markSize).requiredSize(markSize * PREY_MARK_BOX_RATIO),
    )
}
