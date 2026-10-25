package nl.hexmaster.theprey.data.location

import kotlinx.coroutines.flow.Flow

/** One location fix: where, when it was measured (epoch ms, device clock) and how accurate (meters). */
data class LocationFix(
    val latitude: Double,
    val longitude: Double,
    val timeMillis: Long,
    val accuracyMeters: Float? = null,
)

/** What a precise location request reports. */
sealed interface LocationUpdate {
    data class Fix(val fix: LocationFix) : LocationUpdate

    /** Whether the device can currently produce fixes (false when location is switched off). */
    data class Availability(val available: Boolean) : LocationUpdate
}

/** High-accuracy fixes for in-game tracking; the main menu readout uses [LocationSource] instead. */
interface PreciseLocationSource {
    /**
     * Requests high-accuracy fixes every [intervalMs] while collected. Completes when location
     * permission is missing or the request cannot be made.
     */
    fun preciseUpdates(intervalMs: Long): Flow<LocationUpdate>
}
