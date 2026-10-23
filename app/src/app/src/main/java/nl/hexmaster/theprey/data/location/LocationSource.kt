package nl.hexmaster.theprey.data.location

import kotlinx.coroutines.flow.Flow

/** A device position in WGS84 degrees. */
data class GeoPosition(val latitude: Double, val longitude: Double)

/** The device's position as it changes. */
interface LocationSource {
    /**
     * Emits the last known position (if any), then each new fix while collected. Location is
     * only requested while the flow is collected. Completes without emitting when location
     * permission is missing or the device cannot provide a location.
     */
    fun positions(): Flow<GeoPosition>
}
