package nl.hexmaster.theprey.data.sensors

import android.content.Context
import android.hardware.Sensor
import android.hardware.SensorEvent
import android.hardware.SensorEventListener
import android.hardware.SensorManager
import android.view.Surface
import android.view.WindowManager
import kotlinx.coroutines.channels.awaitClose
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.callbackFlow
import kotlinx.coroutines.flow.distinctUntilChanged

/** The direction the device points. */
fun interface HeadingSource {
    /**
     * Emits the device azimuth in degrees clockwise from north, [0, 360), smoothed; null when the
     * device has no usable compass (or it reports unreliable readings). Sensors run only while collected.
     */
    fun headings(): Flow<Float?>
}

/**
 * The compass from the fused rotation-vector sensor (tilt-compensated), falling back to the
 * geomagnetic rotation vector; remapped for the current display rotation.
 */
class CompassHeading(private val context: Context) : HeadingSource {

    override fun headings(): Flow<Float?> = callbackFlow {
        val sensors = context.getSystemService(Context.SENSOR_SERVICE) as? SensorManager
        val sensor = sensors?.getDefaultSensor(Sensor.TYPE_ROTATION_VECTOR)
            ?: sensors?.getDefaultSensor(Sensor.TYPE_GEOMAGNETIC_ROTATION_VECTOR)
        if (sensors == null || sensor == null) {
            trySend(null)
            awaitClose { }
            return@callbackFlow
        }
        val filter = HeadingFilter()
        val rotation = FloatArray(9)
        val remapped = FloatArray(9)
        val orientation = FloatArray(3)
        var reliable = true

        val listener = object : SensorEventListener {
            override fun onSensorChanged(event: SensorEvent) {
                if (!reliable) return
                SensorManager.getRotationMatrixFromVector(rotation, event.values)
                val (x, y) = when (displayRotation()) {
                    Surface.ROTATION_90 -> SensorManager.AXIS_Y to SensorManager.AXIS_MINUS_X
                    Surface.ROTATION_180 -> SensorManager.AXIS_MINUS_X to SensorManager.AXIS_MINUS_Y
                    Surface.ROTATION_270 -> SensorManager.AXIS_MINUS_Y to SensorManager.AXIS_X
                    else -> SensorManager.AXIS_X to SensorManager.AXIS_Y
                }
                SensorManager.remapCoordinateSystem(rotation, x, y, remapped)
                SensorManager.getOrientation(remapped, orientation)
                filter.accept(Math.toDegrees(orientation[0].toDouble()).toFloat())?.let { trySend(it) }
            }

            override fun onAccuracyChanged(sensor: Sensor, accuracy: Int) {
                val wasReliable = reliable
                reliable = accuracy != SensorManager.SENSOR_STATUS_UNRELIABLE
                // Unreliable: let the caller fall back to the direction of travel until it improves.
                if (wasReliable && !reliable) trySend(null)
            }
        }
        sensors.registerListener(listener, sensor, SensorManager.SENSOR_DELAY_UI)
        awaitClose { sensors.unregisterListener(listener) }
    }.distinctUntilChanged()

    @Suppress("DEPRECATION")
    private fun displayRotation(): Int =
        (context.getSystemService(Context.WINDOW_SERVICE) as? WindowManager)?.defaultDisplay?.rotation ?: Surface.ROTATION_0
}
