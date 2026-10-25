package nl.hexmaster.theprey.data.location

import android.Manifest
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class LocationAccessTest {

    private fun grants(vararg permissions: String) = PermissionChecker { it in permissions }

    private val fine = Manifest.permission.ACCESS_FINE_LOCATION
    private val coarse = Manifest.permission.ACCESS_COARSE_LOCATION
    private val background = Manifest.permission.ACCESS_BACKGROUND_LOCATION

    @Test
    fun readLocationAccess_ShouldBeMissing_WhenNothingIsGranted() {
        assertEquals(LocationAccess.Missing, readLocationAccess(grants(), sdkInt = 34))
    }

    @Test
    fun readLocationAccess_ShouldBeApproximateOnly_WhenOnlyCoarseIsGranted() {
        assertEquals(LocationAccess.ApproximateOnly, readLocationAccess(grants(coarse), sdkInt = 34))
    }

    @Test
    fun readLocationAccess_ShouldBeForegroundOnly_WhenPreciseWithoutBackgroundOnApi29Plus() {
        assertEquals(LocationAccess.ForegroundOnly, readLocationAccess(grants(fine, coarse), sdkInt = 29))
    }

    @Test
    fun readLocationAccess_ShouldBeGranted_WhenPreciseAndBackgroundAreGranted() {
        assertEquals(LocationAccess.Granted, readLocationAccess(grants(fine, coarse, background), sdkInt = 34))
    }

    @Test
    fun readLocationAccess_ShouldBeGranted_WhenPreciseIsGrantedBelowApi29() {
        // Below Android 10 a fine grant includes the background.
        assertEquals(LocationAccess.Granted, readLocationAccess(grants(fine), sdkInt = 28))
    }

    @Test
    fun readLocationAccess_ShouldIgnoreBackground_WhenPreciseIsMissing() {
        assertEquals(LocationAccess.ApproximateOnly, readLocationAccess(grants(coarse, background), sdkInt = 34))
    }

    @Test
    fun notificationsAllowed_ShouldFollowThePermission_WhenApi33Plus() {
        assertFalse(notificationsAllowed(grants(), sdkInt = 33))
        assertTrue(notificationsAllowed(grants(Manifest.permission.POST_NOTIFICATIONS), sdkInt = 33))
    }

    @Test
    fun notificationsAllowed_ShouldBeTrue_WhenBelowApi33() {
        assertTrue(notificationsAllowed(grants(), sdkInt = 32))
    }
}
