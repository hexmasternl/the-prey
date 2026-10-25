package nl.hexmaster.theprey.ui.playfields.area

import android.annotation.SuppressLint
import android.graphics.PointF
import android.view.MotionEvent
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.runtime.Composable
import androidx.compose.runtime.DisposableEffect
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberUpdatedState
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.toArgb
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.platform.LocalDensity
import androidx.compose.ui.platform.LocalLayoutDirection
import androidx.compose.ui.unit.dp
import androidx.compose.ui.viewinterop.AndroidView
import androidx.core.view.doOnLayout
import nl.hexmaster.theprey.data.location.GeoPosition
import nl.hexmaster.theprey.ui.components.MapViewLifecycle
import nl.hexmaster.theprey.ui.components.fallbackMapStyle
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
import org.maplibre.android.style.layers.PropertyFactory
import org.maplibre.android.style.sources.GeoJsonSource
import org.maplibre.geojson.Feature
import org.maplibre.geojson.FeatureCollection
import org.maplibre.geojson.Point
import org.maplibre.geojson.Polygon

/**
 * OpenFreeMap's default (Liberty) style: full-colour OpenStreetMap vector tiles, free and without
 * a key. The one place to swap the map provider.
 */
const val MAP_STYLE_URL = "https://tiles.openfreemap.org/styles/liberty"

private const val AREA_SOURCE = "playfield-area"
private const val POINTS_SOURCE = "playfield-points"
private const val FILL_LAYER = "playfield-area-fill"
private const val LINE_LAYER = "playfield-area-line"
private const val POINTS_LAYER = "playfield-points"
private const val SELECTED = "selected"

/** Half of the 48 dp touch target around every point. */
private val HitRadius = 24.dp

/** Room around a fitted area. */
private val FitPadding = 48.dp

/** Gap between the map's attribution button and the chrome below it. */
private val OrnamentGap = 8.dp

/** Theme colours as ARGB ints, the form MapLibre takes. */
private data class MapColors(val signal: Int, val selected: Int, val keyline: Int)

/**
 * The playfield area on a MapLibre map: a green dot per point (red border when selected) and,
 * with 3 or more points, the closed polygon filled at 50 %. Holds no business state: taps and
 * drags are reported through the callbacks, and the map redraws from [points] and [selectedIndex].
 *
 * - A tap near a point (24 dp) reports [onPointTapped], elsewhere [onMapTapped]. Pans and pinches
 *   are never taps.
 * - Pressing on the selected point drags it ([onDragSelected]) instead of panning; every other
 *   gesture goes to the map. Read-only maps ([editable] false) report nothing.
 * - The camera starts from [initialCamera]; with no points it may move once to [locateDevice]'s
 *   position, unless the player moved the map first. It survives rotation.
 *
 * [contentPadding] is the chrome over the map (the bottom bar): fitted areas and the
 * attribution button stay clear of it.
 */
@Composable
// The drag listener is touch-only by nature: drawing an area has no click equivalent.
@SuppressLint("ClickableViewAccessibility")
fun PlayFieldMap(
    points: List<GeoPosition>,
    selectedIndex: Int?,
    editable: Boolean,
    onMapTapped: (GeoPosition) -> Unit,
    onPointTapped: (Int) -> Unit,
    onDragSelected: (GeoPosition) -> Unit,
    locateDevice: suspend () -> GeoPosition?,
    modifier: Modifier = Modifier,
    contentPadding: PaddingValues = PaddingValues(0.dp),
) {
    val context = LocalContext.current
    val density = LocalDensity.current
    val layoutDirection = LocalLayoutDirection.current
    val colors = MapColors(
        signal = ThePreyTheme.colors.signal.toArgb(),
        selected = ThePreyTheme.colors.hunter.toArgb(),
        keyline = ThePreyTheme.colors.void.toArgb(),
    )

    val mapView = remember {
        MapView(
            context,
            MapLibreMapOptions.createFromAttributes(context)
                // A TextureView composes and animates like any other view, unlike a SurfaceView.
                .textureMode(true)
                .compassEnabled(false)
                .rotateGesturesEnabled(false)
                .tiltGesturesEnabled(false),
        )
    }
    var map by remember { mutableStateOf<MapLibreMap?>(null) }
    var style by remember { mutableStateOf<Style?>(null) }

    // [latitude, longitude, zoom] of the last camera at rest, so a rotation keeps the view.
    var savedCamera by rememberSaveable { mutableStateOf<DoubleArray?>(null) }
    var userMoved by rememberSaveable { mutableStateOf(false) }
    val firstOpen = remember { savedCamera == null }
    val startCamera = remember { initialCamera(points, devicePosition = null) }
    var devicePosition by remember { mutableStateOf<GeoPosition?>(null) }

    val currentPoints by rememberUpdatedState(points)
    val currentSelected by rememberUpdatedState(selectedIndex)
    val currentEditable by rememberUpdatedState(editable)
    val currentOnMapTapped by rememberUpdatedState(onMapTapped)
    val currentOnPointTapped by rememberUpdatedState(onPointTapped)
    val currentOnDragSelected by rememberUpdatedState(onDragSelected)

    val hitRadiusPx = with(density) { HitRadius.toPx() }
    val fitPaddingPx = with(density) { FitPadding.roundToPx() }
    val gapPx = with(density) { OrnamentGap.roundToPx() }
    val padStart = with(density) { contentPadding.calculateLeftPadding(layoutDirection).roundToPx() }
    val padTop = with(density) { contentPadding.calculateTopPadding().roundToPx() }
    val padEnd = with(density) { contentPadding.calculateRightPadding(layoutDirection).roundToPx() }
    val padBottom = with(density) { contentPadding.calculateBottomPadding().roundToPx() }

    MapViewLifecycle(mapView)

    DisposableEffect(mapView) {
        var fallbackApplied = false
        val onStyleLoaded = Style.OnStyleLoaded { loaded ->
            addAreaLayers(loaded, colors)
            style = loaded
        }
        // No style (offline, provider down): a plain Void background, so points can still be placed.
        val onFailed = MapView.OnDidFailLoadingMapListener {
            if (!fallbackApplied) {
                fallbackApplied = true
                map?.setStyle(Style.Builder().fromJson(fallbackMapStyle(colors.keyline)), onStyleLoaded)
            }
        }
        mapView.addOnDidFailLoadingMapListener(onFailed)
        mapView.getMapAsync { m ->
            m.uiSettings.apply {
                isRotateGesturesEnabled = false
                isTiltGesturesEnabled = false
                isCompassEnabled = false
                isDoubleTapGesturesEnabled = true
                // Attribution stays: the tile licence requires it. The MapLibre logo is optional.
                isAttributionEnabled = true
                isLogoEnabled = false
            }
            m.addOnCameraMoveStartedListener { reason ->
                if (reason == MapLibreMap.OnCameraMoveStartedListener.REASON_API_GESTURE) userMoved = true
            }
            m.addOnCameraIdleListener {
                val camera = m.cameraPosition
                camera.target?.let { savedCamera = doubleArrayOf(it.latitude, it.longitude, camera.zoom) }
            }
            m.addOnMapClickListener { latLng ->
                if (currentEditable) {
                    val projection = m.projection
                    val tap = projection.toScreenLocation(latLng).toScreenPoint()
                    val onScreen = currentPoints.map { projection.toScreenLocation(it.toLatLng()).toScreenPoint() }
                    val hit = hitTest(onScreen, tap, hitRadiusPx)
                    if (hit != null) currentOnPointTapped(hit) else currentOnMapTapped(latLng.toGeoPosition())
                }
                true
            }
            mapView.doOnLayout {
                val saved = savedCamera
                if (saved != null) {
                    m.moveCamera(CameraUpdateFactory.newLatLngZoom(LatLng(saved[0], saved[1]), saved[2]))
                } else {
                    m.moveCamera(
                        when (val camera = startCamera) {
                            is MapCamera.Fit -> CameraUpdateFactory.newLatLngBounds(
                                LatLngBounds.Builder()
                                    .include(camera.southWest.toLatLng())
                                    .include(camera.northEast.toLatLng())
                                    .build(),
                                padStart + fitPaddingPx,
                                padTop + fitPaddingPx,
                                padEnd + fitPaddingPx,
                                padBottom + fitPaddingPx,
                            )
                            is MapCamera.Center -> CameraUpdateFactory.newLatLngZoom(camera.target.toLatLng(), camera.zoom)
                        },
                    )
                }
            }
            m.setStyle(Style.Builder().fromUri(MAP_STYLE_URL), onStyleLoaded)
            map = m
        }
        onDispose { mapView.removeOnDidFailLoadingMapListener(onFailed) }
    }

    // Drag the selected point. Runs before MapLibre's gesture detector: only a press on the
    // selected point is taken; everything else (pan, pinch, a press on another point) passes on.
    DisposableEffect(mapView) {
        var dragging = false
        val listener = android.view.View.OnTouchListener { view, event ->
            val m = map
            when (event.actionMasked) {
                MotionEvent.ACTION_DOWN -> {
                    val index = currentSelected
                    val points = currentPoints
                    dragging = m != null && currentEditable && index != null && index in points.indices &&
                        hitTest(
                            listOf(m.projection.toScreenLocation(points[index].toLatLng()).toScreenPoint()),
                            ScreenPoint(event.x, event.y),
                            hitRadiusPx,
                        ) != null
                    if (dragging) view.parent?.requestDisallowInterceptTouchEvent(true)
                    dragging
                }
                MotionEvent.ACTION_MOVE -> {
                    if (dragging && m != null) {
                        currentOnDragSelected(m.projection.fromScreenLocation(PointF(event.x, event.y)).toGeoPosition())
                    }
                    dragging
                }
                MotionEvent.ACTION_UP, MotionEvent.ACTION_CANCEL -> {
                    val wasDragging = dragging
                    dragging = false
                    wasDragging
                }
                else -> dragging
            }
        }
        mapView.setOnTouchListener(listener)
        onDispose { mapView.setOnTouchListener(null) }
    }

    // Only on the first open of an area without points, and only with permission already granted
    // (the caller decides); a rotation never re-runs it.
    LaunchedEffect(Unit) {
        if (!firstOpen || points.isNotEmpty()) return@LaunchedEffect
        devicePosition = locateDevice()
    }
    LaunchedEffect(map, devicePosition) {
        val m = map ?: return@LaunchedEffect
        val position = devicePosition ?: return@LaunchedEffect
        devicePosition = null
        if (!userMoved && currentPoints.isEmpty()) {
            m.animateCamera(CameraUpdateFactory.newLatLngZoom(position.toLatLng(), STREET_ZOOM))
        }
    }

    LaunchedEffect(map, padBottom) {
        val settings = map?.uiSettings ?: return@LaunchedEffect
        settings.setAttributionMargins(
            settings.attributionMarginLeft,
            settings.attributionMarginTop,
            settings.attributionMarginRight,
            padBottom + gapPx,
        )
    }

    LaunchedEffect(style, points, selectedIndex) {
        val loaded = style ?: return@LaunchedEffect
        loaded.getSourceAs<GeoJsonSource>(AREA_SOURCE)?.setGeoJson(areaFeatures(points))
        loaded.getSourceAs<GeoJsonSource>(POINTS_SOURCE)?.setGeoJson(pointFeatures(points, selectedIndex))
    }

    AndroidView(factory = { mapView }, modifier = modifier)
}

private fun addAreaLayers(style: Style, colors: MapColors) {
    style.addSource(GeoJsonSource(AREA_SOURCE))
    style.addSource(GeoJsonSource(POINTS_SOURCE))
    style.addLayer(
        FillLayer(FILL_LAYER, AREA_SOURCE).withProperties(
            PropertyFactory.fillColor(colors.signal),
            PropertyFactory.fillOpacity(0.5f),
        ),
    )
    style.addLayer(
        LineLayer(LINE_LAYER, AREA_SOURCE).withProperties(
            PropertyFactory.lineColor(colors.signal),
            PropertyFactory.lineWidth(2f),
        ),
    )
    style.addLayer(
        CircleLayer(POINTS_LAYER, POINTS_SOURCE).withProperties(
            PropertyFactory.circleColor(colors.signal),
            // 20 dp marker with a 3 dp keyline: Void normally, Hunter when selected.
            PropertyFactory.circleRadius(10f),
            PropertyFactory.circleStrokeWidth(3f),
            PropertyFactory.circleStrokeColor(
                Expression.switchCase(
                    Expression.eq(Expression.get(SELECTED), Expression.literal(true)),
                    Expression.color(colors.selected),
                    Expression.color(colors.keyline),
                ),
            ),
        ),
    )
}

/** The closed polygon (the last point connects back to the first), only with 3 or more points. */
private fun areaFeatures(points: List<GeoPosition>): FeatureCollection =
    if (points.size < MIN_AREA_POINTS) {
        FeatureCollection.fromFeatures(emptyList())
    } else {
        val ring = (points + points.first()).map { it.toPoint() }
        FeatureCollection.fromFeature(Feature.fromGeometry(Polygon.fromLngLats(listOf(ring))))
    }

private fun pointFeatures(points: List<GeoPosition>, selectedIndex: Int?): FeatureCollection =
    FeatureCollection.fromFeatures(
        points.mapIndexed { index, position ->
            Feature.fromGeometry(position.toPoint()).apply { addBooleanProperty(SELECTED, index == selectedIndex) }
        },
    )

private fun GeoPosition.toLatLng(): LatLng = LatLng(latitude, longitude)

private fun GeoPosition.toPoint(): Point = Point.fromLngLat(longitude, latitude)

private fun LatLng.toGeoPosition(): GeoPosition = GeoPosition(latitude, longitude)

private fun PointF.toScreenPoint(): ScreenPoint = ScreenPoint(x, y)
