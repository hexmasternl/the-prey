package nl.hexmaster.theprey.data.location

import android.content.Context
import android.os.Looper
import com.google.android.gms.location.FusedLocationProviderClient
import com.google.android.gms.location.LocationCallback
import com.google.android.gms.location.LocationRequest
import com.google.android.gms.location.LocationResult
import com.google.android.gms.location.LocationServices
import com.google.android.gms.location.Priority
import kotlinx.coroutines.channels.awaitClose
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.callbackFlow

private const val UPDATE_INTERVAL_MS = 10_000L

/** [LocationSource] backed by Google Play services' fused location provider. */
class FusedLocationSource(context: Context) : LocationSource {

    private val client: FusedLocationProviderClient = LocationServices.getFusedLocationProviderClient(context)

    override fun positions(): Flow<GeoPosition> = callbackFlow {
        val callback = object : LocationCallback() {
            override fun onLocationResult(result: LocationResult) {
                result.lastLocation?.let { trySend(GeoPosition(it.latitude, it.longitude)) }
            }
        }
        try {
            client.lastLocation.addOnSuccessListener { location ->
                location?.let { trySend(GeoPosition(it.latitude, it.longitude)) }
            }
            val request = LocationRequest.Builder(Priority.PRIORITY_BALANCED_POWER_ACCURACY, UPDATE_INTERVAL_MS).build()
            client.requestLocationUpdates(request, callback, Looper.getMainLooper())
                // Play services unavailable or location off: no position, the caller shows "no signal".
                .addOnFailureListener { close() }
        } catch (_: SecurityException) {
            // Permission missing or revoked: same outcome.
            close()
        }
        awaitClose { client.removeLocationUpdates(callback) }
    }
}
