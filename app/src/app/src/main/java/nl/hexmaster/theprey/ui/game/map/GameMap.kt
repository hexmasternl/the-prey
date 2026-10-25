package nl.hexmaster.theprey.ui.game.map

import android.os.SystemClock
import androidx.compose.runtime.Composable
import androidx.compose.runtime.DisposableEffect
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableFloatStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberUpdatedState
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.toArgb
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.platform.LocalDensity
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.unit.dp
import androidx.compose.ui.viewinterop.AndroidView
import androidx.core.content.ContextCompat
import androidx.core.graphics.drawable.toBitmap
import androidx.core.view.doOnLayout
import kotlinx.coroutines.delay
import kotlinx.coroutines.isActive
import nl.hexmaster.theprey.R
import nl.hexmaster.theprey.data.location.GeoPosition
import nl.hexmaster.theprey.ui.components.MapViewLifecycle
import nl.hexmaster.theprey.ui.components.fallbackMapStyle
import nl.hexmaster.theprey.ui.game.GameRole
import nl.hexmaster.theprey.ui.playfields.area.MAP_STYLE_URL
import nl.hexmaster.theprey.ui.theme.ThePreyTheme
import org.maplibre.android.camera.CameraUpdateFactory
import org.maplibre.android.geometry.LatLng
import org.maplibre.android.geometry.LatLngBounds
import org.maplibre.android.maps.MapLibreMap
import org.maplibre.android.maps.MapLibreMapOptions
import org.maplibre.android.maps.MapView
import org.maplibre.android.maps.Style
import org.maplibre.android.style.expressions.Expression
import org.maplibre.android.style.layers.CircleLayer
import org.maplibre.android.style.layers.FillLayer
import org.maplibre.android.style.layers.LineLayer
import org.maplibre.android.style.layers.Property
import org.maplibre.android.style.layers.PropertyFactory
import org.maplibre.android.style.layers.SymbolLayer
import org.maplibre.android.style.sources.GeoJsonSource
import org.maplibre.geojson.Feature
import org.maplibre.geojson.FeatureCollection
import org.maplibre.geojson.Point
import org.maplibre.geojson.Polygon
import java.util.Locale

private const val AREA_SOURCE = "game-area"
private const val AREA_FILL = "game-area-fill"
private const val AREA_LINE = "game-area-line"
private const val PARTICIPANTS_SOURCE = "game-participants"
private const val PARTICIPANTS_LAYER = "game-participants"
private const val RING_LAYER = "game-participants-ring"
private const val SELF_SOURCE = "game-self"
private const val SELF_LAYER = "game-self"
private const val ARROW_IMAGE = "game-player-arrow"

/** Street-level zoom for the first device fix. */
private const val FOLLOW_ZOOM = 17.0
private const val EASE_MS = 300
private const val RECENTER_CHECK_MS = 250L
private const val PULSE_TICK_MS = 100L
private val FitPadding = 48.dp

/** One other participant on the map. */
data class MapMarker(val userId: String, val position: GeoPosition, val style: MarkerStyle)

/**
 * The in-game map: the playfield area in the viewer's role colour, the other participants as
 * styled dots, and the player as a compass-driven arrow. North-up; pinch and pan move it, and
 * [RecenterTimer] brings it back to the player 5 s after the last touch. [contentDescription]
 * describes it for screen readers.
 */
@Composable
fun GameMap(
    area: List<GeoPosition>,
    areaRole: GameRole,
    markers: List<MapMarker>,
    own: GeoPosition?,
    heading: Float,
    reducedMotion: Boolean,
    contentDescription: String,
    modifier: Modifier = Modifier,
) {
    val context = LocalContext.current
    val density = LocalDensity.current
    val colors = ThePreyTheme.colors
    val palette = MarkerPalette(
        prey = colors.prey.toArgb(),
        hunter = colors.hunter.toArgb(),
        ghost = colors.textGhost.toArgb(),
        keyline = colors.void.toArgb(),
    )
    val areaColor = if (areaRole == GameRole.Hunter) palette.hunter else palette.prey

    val mapView = remember {
        MapView(
            context,
            MapLibreMapOptions.createFromAttributes(context)
                .textureMode(true)
                .compassEnabled(false)
                .rotateGesturesEnabled(false)
                .tiltGesturesEnabled(false),
        )
    }
    var map by remember { mutableStateOf<MapLibreMap?>(null) }
    var style by remember { mutableStateOf<Style?>(null) }
    val timer = remember { RecenterTimer() }
    var centeredOnce by remember { mutableStateOf(false) }
    var pulsePhase by remember { mutableFloatStateOf(0f) }
    val currentOwn by rememberUpdatedState(own)
    val currentArea by rememberUpdatedState(area)
    val fitPaddingPx = with(density) { FitPadding.roundToPx() }

    MapViewLifecycle(mapView)

    DisposableEffect(mapView) {
        var fallbackApplied = false
        val onStyleLoaded = Style.OnStyleLoaded { loaded ->
            addGameLayers(loaded, palette, ContextCompat.getDrawable(context, R.drawable.ic_game_player_arrow)?.toBitmap())
            style = loaded
        }
        // No tiles (offline): the area, markers and arrow still draw on a Void background.
        val onFailed = MapView.OnDidFailLoadingMapListener {
            if (!fallbackApplied) {
                fallbackApplied = true
                map?.setStyle(Style.Builder().fromJson(fallbackMapStyle(palette.keyline)), onStyleLoaded)
            }
        }
        mapView.addOnDidFailLoadingMapListener(onFailed)
        mapView.getMapAsync { m ->
            m.uiSettings.apply {
                isRotateGesturesEnabled = false
                isTiltGesturesEnabled = false
                isCompassEnabled = false
                isAttributionEnabled = true
                isLogoEnabled = false
            }
            var gestureActive = false
            m.addOnCameraMoveStartedListener { reason ->
                // Only the player's own touches count; the code's own camera moves never restart the wait.
                gestureActive = reason == MapLibreMap.OnCameraMoveStartedListener.REASON_API_GESTURE
                if (gestureActive) timer.onGesture(SystemClock.elapsedRealtime())
            }
            m.addOnCameraMoveListener { if (gestureActive) timer.onGesture(SystemClock.elapsedRealtime()) }
            m.addOnCameraIdleListener { gestureActive = false }
            mapView.doOnLayout {
                val fix = currentOwn
                if (fix != null) {
                    m.moveCamera(CameraUpdateFactory.newLatLngZoom(fix.toLatLng(), FOLLOW_ZOOM))
                    centeredOnce = true
                } else {
                    areaBounds(currentArea)?.let { m.moveCamera(CameraUpdateFactory.newLatLngBounds(it, fitPaddingPx)) }
                }
            }
            m.setStyle(Style.Builder().fromUri(MAP_STYLE_URL), onStyleLoaded)
            map = m
        }
        onDispose { mapView.removeOnDidFailLoadingMapListener(onFailed) }
    }

    // Follow the device while nobody touches the map; the first fix also sets street-level zoom.
    LaunchedEffect(map, own) {
        val m = map ?: return@LaunchedEffect
        val fix = own ?: return@LaunchedEffect
        if (!timer.isFollowing) return@LaunchedEffect
        if (!centeredOnce) {
            centeredOnce = true
            m.moveCamera(CameraUpdateFactory.newLatLngZoom(fix.toLatLng(), FOLLOW_ZOOM))
        } else {
            m.moveTo(fix, reducedMotion)
        }
    }

    // Back to the player 5 s after the last touch, keeping the zoom the player chose.
    LaunchedEffect(map, reducedMotion) {
        val m = map ?: return@LaunchedEffect
        while (isActive) {
            delay(RECENTER_CHECK_MS)
            if (timer.shouldRecenter(SystemClock.elapsedRealtime())) currentOwn?.let { m.moveTo(it, reducedMotion) }
        }
    }

    val hasPulse = markers.any { it.style.pulse }
    LaunchedEffect(hasPulse, reducedMotion) {
        if (!hasPulse || reducedMotion) return@LaunchedEffect
        while (isActive) {
            pulsePhase = (SystemClock.elapsedRealtime() % PULSE_PERIOD_MS) / PULSE_PERIOD_MS.toFloat()
            delay(PULSE_TICK_MS)
        }
    }

    LaunchedEffect(style, area, areaColor) {
        val loaded = style ?: return@LaunchedEffect
        loaded.getSourceAs<GeoJsonSource>(AREA_SOURCE)?.setGeoJson(areaFeatures(area))
        (loaded.getLayer(AREA_FILL) as? FillLayer)?.setProperties(PropertyFactory.fillColor(areaColor))
        (loaded.getLayer(AREA_LINE) as? LineLayer)?.setProperties(PropertyFactory.lineColor(areaColor))
    }
    LaunchedEffect(style, markers, pulsePhase, reducedMotion) {
        val loaded = style ?: return@LaunchedEffect
        loaded.getSourceAs<GeoJsonSource>(PARTICIPANTS_SOURCE)
            ?.setGeoJson(participantFeatures(markers, palette, pulsePhase, reducedMotion))
    }
    LaunchedEffect(style, own, heading) {
        val loaded = style ?: return@LaunchedEffect
        loaded.getSourceAs<GeoJsonSource>(SELF_SOURCE)?.setGeoJson(selfFeatures(own, heading))
    }

    AndroidView(
        factory = { mapView },
        modifier = modifier.semantics { this.contentDescription = contentDescription },
    )
}

/** Marker colours as ARGB ints, the form MapLibre takes. */
internal data class MarkerPalette(val prey: Int, val hunter: Int, val ghost: Int, val keyline: Int) {
    fun of(color: MarkerColor): Int = when (color) {
        MarkerColor.Prey -> prey
        MarkerColor.Hunter -> hunter
        MarkerColor.Ghost -> ghost
    }
}

/** How a marker is painted at one moment: what the participant layer's data-driven paint reads. */
internal data class MarkerPaint(
    val fill: Int,
    val stroke: Int,
    val strokeWidth: Float,
    val radius: Float,
    val opacity: Float,
    /** Draw the steady ring of a penalised prey (reduced motion). */
    val ring: Boolean,
)

internal const val PULSE_PERIOD_MS = 1_200L
internal const val DOT_RADIUS = 6f
private const val PULSE_GROWTH = 4f

/**
 * The paint for [style] at [pulsePhase] (0..1). Dots without a ring get a 1 dp Void keyline so
 * green dots stay visible over the green area. A pulse grows and fades the dot; under
 * [reducedMotion] it stays steady and gets a ring instead.
 */
internal fun markerPaint(style: MarkerStyle, palette: MarkerPalette, pulsePhase: Float, reducedMotion: Boolean): MarkerPaint {
    val wave = if (style.pulse && !reducedMotion) kotlin.math.sin(Math.PI * pulsePhase).toFloat() else 0f
    return MarkerPaint(
        fill = palette.of(style.fill),
        stroke = style.stroke?.let(palette::of) ?: palette.keyline,
        strokeWidth = if (style.stroke != null) 2f else 1f,
        radius = DOT_RADIUS + PULSE_GROWTH * wave,
        opacity = 1f - 0.5f * wave,
        ring = style.pulse && reducedMotion,
    )
}

private fun addGameLayers(style: Style, palette: MarkerPalette, arrow: android.graphics.Bitmap?) {
    style.addSource(GeoJsonSource(AREA_SOURCE))
    style.addSource(GeoJsonSource(PARTICIPANTS_SOURCE))
    style.addSource(GeoJsonSource(SELF_SOURCE))
    arrow?.let { style.addImage(ARROW_IMAGE, it) }
    style.addLayer(
        FillLayer(AREA_FILL, AREA_SOURCE).withProperties(
            PropertyFactory.fillColor(palette.prey),
            PropertyFactory.fillOpacity(0.15f),
        ),
    )
    style.addLayer(
        LineLayer(AREA_LINE, AREA_SOURCE).withProperties(
            PropertyFactory.lineColor(palette.prey),
            PropertyFactory.lineWidth(2f),
        ),
    )
    style.addLayer(
        CircleLayer(RING_LAYER, PARTICIPANTS_SOURCE)
            .withFilter(Expression.eq(Expression.get("ring"), Expression.literal(true)))
            .withProperties(
                PropertyFactory.circleRadius(12f),
                PropertyFactory.circleOpacity(0f),
                PropertyFactory.circleStrokeColor(palette.hunter),
                PropertyFactory.circleStrokeWidth(2f),
            ),
    )
    style.addLayer(
        CircleLayer(PARTICIPANTS_LAYER, PARTICIPANTS_SOURCE).withProperties(
            PropertyFactory.circleColor(Expression.toColor(Expression.get("fill"))),
            PropertyFactory.circleStrokeColor(Expression.toColor(Expression.get("stroke"))),
            PropertyFactory.circleStrokeWidth(Expression.get("strokeWidth")),
            PropertyFactory.circleRadius(Expression.get("radius")),
            PropertyFactory.circleOpacity(Expression.get("opacity")),
            PropertyFactory.circleStrokeOpacity(Expression.get("opacity")),
        ),
    )
    style.addLayer(
        SymbolLayer(SELF_LAYER, SELF_SOURCE).withProperties(
            PropertyFactory.iconImage(ARROW_IMAGE),
            PropertyFactory.iconRotate(Expression.get("heading")),
            // The arrow keeps its true bearing on the north-up map.
            PropertyFactory.iconRotationAlignment(Property.ICON_ROTATION_ALIGNMENT_MAP),
            PropertyFactory.iconAllowOverlap(true),
            PropertyFactory.iconIgnorePlacement(true),
        ),
    )
}

private fun areaFeatures(area: List<GeoPosition>): FeatureCollection =
    if (area.size < 3) {
        FeatureCollection.fromFeatures(emptyList())
    } else {
        val ring = (area + area.first()).map { it.toPoint() }
        FeatureCollection.fromFeature(Feature.fromGeometry(Polygon.fromLngLats(listOf(ring))))
    }

private fun participantFeatures(
    markers: List<MapMarker>,
    palette: MarkerPalette,
    pulsePhase: Float,
    reducedMotion: Boolean,
): FeatureCollection = FeatureCollection.fromFeatures(
    markers.map { marker ->
        val paint = markerPaint(marker.style, palette, pulsePhase, reducedMotion)
        Feature.fromGeometry(marker.position.toPoint()).apply {
            addStringProperty("fill", paint.fill.toHex())
            addStringProperty("stroke", paint.stroke.toHex())
            addNumberProperty("strokeWidth", paint.strokeWidth)
            addNumberProperty("radius", paint.radius)
            addNumberProperty("opacity", paint.opacity)
            addBooleanProperty("ring", paint.ring)
        }
    },
)

private fun selfFeatures(own: GeoPosition?, heading: Float): FeatureCollection =
    if (own == null) {
        FeatureCollection.fromFeatures(emptyList())
    } else {
        FeatureCollection.fromFeature(Feature.fromGeometry(own.toPoint()).apply { addNumberProperty("heading", heading) })
    }

private fun areaBounds(area: List<GeoPosition>): LatLngBounds? {
    if (area.distinct().size < 2) return null
    return LatLngBounds.Builder().apply { area.forEach { include(it.toLatLng()) } }.build()
}

private fun MapLibreMap.moveTo(position: GeoPosition, reducedMotion: Boolean) {
    val update = CameraUpdateFactory.newLatLng(position.toLatLng())
    if (reducedMotion) moveCamera(update) else easeCamera(update, EASE_MS)
}

private fun Int.toHex(): String = String.format(Locale.ROOT, "#%06X", this and 0xFFFFFF)

private fun GeoPosition.toLatLng(): LatLng = LatLng(latitude, longitude)

private fun GeoPosition.toPoint(): Point = Point.fromLngLat(longitude, latitude)
