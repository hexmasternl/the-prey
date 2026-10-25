package nl.hexmaster.theprey.ui.settings

import nl.hexmaster.theprey.data.location.LocationAccess
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class GrantStepTest {

    @Test
    fun grantStep_ShouldRequestPrecise_WhenMissingAndThePromptIsAvailable() {
        assertEquals(GrantStep.RequestPrecise, grantStep(LocationAccess.Missing, canAskFine = true))
    }

    @Test
    fun grantStep_ShouldOpenSettingsForPrecise_WhenMissingAndPermanentlyDenied() {
        assertEquals(GrantStep.OpenSettingsForPrecise, grantStep(LocationAccess.Missing, canAskFine = false))
    }

    @Test
    fun grantStep_ShouldRequestPrecise_WhenApproximateOnlyAndThePromptIsAvailable() {
        assertEquals(GrantStep.RequestPrecise, grantStep(LocationAccess.ApproximateOnly, canAskFine = true))
    }

    @Test
    fun grantStep_ShouldOpenSettingsForPrecise_WhenApproximateOnlyAndPermanentlyDenied() {
        assertEquals(GrantStep.OpenSettingsForPrecise, grantStep(LocationAccess.ApproximateOnly, canAskFine = false))
    }

    @Test
    fun grantStep_ShouldOpenSettingsForAllTheTime_WhenForegroundOnly() {
        assertEquals(GrantStep.OpenSettingsForAllTheTime, grantStep(LocationAccess.ForegroundOnly, canAskFine = true))
        assertEquals(GrantStep.OpenSettingsForAllTheTime, grantStep(LocationAccess.ForegroundOnly, canAskFine = false))
    }

    @Test
    fun grantStep_ShouldBeNone_WhenGranted() {
        assertEquals(GrantStep.None, grantStep(LocationAccess.Granted, canAskFine = true))
        assertEquals(GrantStep.None, grantStep(LocationAccess.Granted, canAskFine = false))
    }

    @Test
    fun canAskFine_ShouldBeTrue_BeforeTheFirstRequestThroughTheDisclosure() {
        // "Not now" on the main menu: the disclosure was never accepted, the system never asked.
        assertTrue(canAskFine(rationale = false, disclosureAccepted = false))
    }

    @Test
    fun canAskFine_ShouldBeTrue_WhenDeniedOnce() {
        assertTrue(canAskFine(rationale = true, disclosureAccepted = true))
    }

    @Test
    fun canAskFine_ShouldBeFalse_WhenDeniedForGood() {
        assertFalse(canAskFine(rationale = false, disclosureAccepted = true))
    }
}
