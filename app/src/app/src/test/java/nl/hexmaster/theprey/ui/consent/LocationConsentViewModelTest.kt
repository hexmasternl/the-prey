package nl.hexmaster.theprey.ui.consent

import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.launch
import kotlinx.coroutines.test.TestScope
import kotlinx.coroutines.test.UnconfinedTestDispatcher
import kotlinx.coroutines.test.resetMain
import kotlinx.coroutines.test.runTest
import kotlinx.coroutines.test.setMain
import nl.hexmaster.theprey.data.location.LocationConsentStore
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test

@OptIn(ExperimentalCoroutinesApi::class)
class LocationConsentViewModelTest {

    private class FakeConsentStore(accepted: Boolean = false, menuPromptShown: Boolean = false) : LocationConsentStore {
        override val disclosureAccepted = MutableStateFlow(accepted)
        override val menuPromptShown = MutableStateFlow(menuPromptShown)

        override suspend fun setDisclosureAccepted(accepted: Boolean) {
            disclosureAccepted.value = accepted
        }

        override suspend fun setMenuPromptShown() {
            menuPromptShown.value = true
        }
    }

    private val store = FakeConsentStore()

    @Before
    fun setUp() = Dispatchers.setMain(UnconfinedTestDispatcher())

    @After
    fun tearDown() = Dispatchers.resetMain()

    private fun consent(store: LocationConsentStore = this.store, sdkInt: Int = 34) =
        LocationConsentViewModel(store, sdkInt)

    /** Signed in on the main menu, permission checked and missing: the state right after a first sign-in. */
    private fun signedInWithoutPermission(sut: LocationConsentViewModel, canAsk: Boolean = false) {
        sut.onSessionChanged(signedIn = true)
        sut.onMainMenuVisible(true)
        sut.onPermissionChecked(granted = false, canAsk = canAsk)
    }

    /** Collects every effect for the rest of the test. */
    private fun TestScope.effectsOf(sut: LocationConsentViewModel): List<ConsentEffect> {
        val effects = mutableListOf<ConsentEffect>()
        backgroundScope.launch(UnconfinedTestDispatcher(testScheduler)) { sut.effects.collect { effects += it } }
        return effects
    }

    private fun LocationConsentViewModel.deny(canAskAgain: Boolean, coarse: Boolean = false) =
        onPermissionResult(fineGranted = false, coarseGranted = coarse, canAskAgain = canAskAgain, notificationsGranted = true)

    private fun LocationConsentViewModel.allow() =
        onPermissionResult(fineGranted = true, coarseGranted = true, canAskAgain = false, notificationsGranted = true)

    @Test
    fun uiState_ShouldShowDisclosure_WhenFirstSignedInWithoutPermission() {
        val sut = consent()

        signedInWithoutPermission(sut)

        assertEquals(ConsentUiState.Disclosure, sut.uiState.value)
    }

    @Test
    fun uiState_ShouldStayHidden_WhenSignedOut() {
        val sut = consent()

        sut.onSessionChanged(signedIn = false)
        sut.onPermissionChecked(granted = false, canAsk = false)

        assertEquals(ConsentUiState.Hidden, sut.uiState.value)
    }

    @Test
    fun uiState_ShouldBeHidden_WhenPermissionIsGranted() {
        val sut = consent(FakeConsentStore(accepted = true))

        sut.onSessionChanged(signedIn = true)
        sut.onPermissionChecked(granted = true, canAsk = false)

        assertEquals(ConsentUiState.Hidden, sut.uiState.value)
        assertTrue(sut.granted.value)
        assertTrue(sut.canReadLocation.value)
    }

    @Test
    fun uiState_ShouldStayHidden_WhenPermissionIsRevokedAfterTheMenuAsked() {
        val sut = consent(FakeConsentStore(accepted = true, menuPromptShown = true))
        sut.onSessionChanged(signedIn = true)
        sut.onMainMenuVisible(true)
        sut.onPermissionChecked(granted = true, canAsk = false)

        sut.onPermissionChecked(granted = false, canAsk = true)

        assertEquals(ConsentUiState.Hidden, sut.uiState.value)
        assertFalse(sut.granted.value)
    }

    @Test
    fun onContinue_ShouldRecordAcceptanceAndRequestLocation_WhenDisclosureIsShown() = runTest {
        val sut = consent()
        val effects = effectsOf(sut)
        signedInWithoutPermission(sut)

        sut.onContinue()

        assertTrue(store.disclosureAccepted.first())
        assertEquals(listOf(ConsentEffect.RequestLocation), effects)
        assertEquals(ConsentUiState.Hidden, sut.uiState.value)
    }

    @Test
    fun onContinue_ShouldNotRequestLocation_WhenDisclosureIsNotShown() = runTest {
        val sut = consent()
        val effects = effectsOf(sut)

        sut.onContinue()

        assertTrue(effects.isEmpty())
        assertFalse(store.disclosureAccepted.first())
    }

    @Test
    fun uiState_ShouldStayHidden_WhenResumedWhileThePromptIsOpen() {
        val sut = consent()
        signedInWithoutPermission(sut)
        sut.onContinue()

        // The system prompt pauses and resumes the activity before its answer arrives.
        sut.onPermissionChecked(granted = false, canAsk = false)

        assertEquals(ConsentUiState.Hidden, sut.uiState.value)
    }

    @Test
    fun onNotNow_ShouldHideWithoutPrompting_WhenDisclosureIsShown() = runTest {
        val sut = consent()
        val effects = effectsOf(sut)
        signedInWithoutPermission(sut)

        sut.onNotNow()
        sut.onPermissionChecked(granted = false, canAsk = false)

        assertEquals(ConsentUiState.Hidden, sut.uiState.value)
        assertTrue(effects.isEmpty())
        assertFalse(store.disclosureAccepted.first())
        assertFalse(sut.canReadLocation.value)
    }

    @Test
    fun uiState_ShouldStayHidden_WhenAppRestartsAfterNotNow() {
        val first = consent()
        signedInWithoutPermission(first)
        first.onNotNow()

        val restarted = consent()
        signedInWithoutPermission(restarted)

        assertEquals(ConsentUiState.Hidden, restarted.uiState.value)
    }

    @Test
    fun uiState_ShouldStayHidden_WhenSignedInAgainAfterNotNow() {
        val sut = consent()
        signedInWithoutPermission(sut)
        sut.onNotNow()

        sut.onSessionChanged(signedIn = false)
        sut.onSessionChanged(signedIn = true)

        assertEquals(ConsentUiState.Hidden, sut.uiState.value)
    }

    @Test
    fun uiState_ShouldHide_WhenFirstPromptIsDenied() {
        val sut = consent()
        signedInWithoutPermission(sut)
        sut.onContinue()

        sut.deny(canAskAgain = true)

        assertEquals(ConsentUiState.Hidden, sut.uiState.value)
        assertFalse(sut.canReadLocation.value)
    }

    @Test
    fun uiState_ShouldShowOpenSettings_WhenDeniedAfterAcceptanceWithoutRationale() {
        val sut = consent()
        signedInWithoutPermission(sut)
        sut.onContinue()

        sut.deny(canAskAgain = false)

        assertEquals(ConsentUiState.OpenSettings, sut.uiState.value)
    }

    @Test
    fun uiState_ShouldShowOpenSettings_WhenOnlyApproximateLocationIsGranted() {
        val sut = consent()
        signedInWithoutPermission(sut)
        sut.onContinue()

        sut.deny(canAskAgain = true, coarse = true)

        assertEquals(ConsentUiState.OpenSettings, sut.uiState.value)
        assertFalse(sut.granted.value)
        assertTrue(sut.canReadLocation.value)
    }

    @Test
    fun uiState_ShouldShowDisclosure_WhenApproximateWasGrantedBeforeTheDisclosure() {
        val sut = consent()

        sut.onSessionChanged(signedIn = true)
        sut.onMainMenuVisible(true)
        sut.onPermissionChecked(granted = false, canAsk = false, approximate = true)

        assertEquals(ConsentUiState.Disclosure, sut.uiState.value)
    }

    @Test
    fun onOpenSettings_ShouldOpenAppSettings_WhenSettingsVariantIsShown() = runTest {
        val sut = consent()
        val effects = effectsOf(sut)
        signedInWithoutPermission(sut)
        sut.onContinue()
        sut.deny(canAskAgain = false)

        sut.onOpenSettings()

        assertEquals(ConsentEffect.OpenAppSettings, effects.last())
    }

    @Test
    fun uiState_ShouldHide_WhenPermissionIsGrantedInSettings() {
        val sut = consent()
        signedInWithoutPermission(sut)
        sut.onContinue()
        sut.deny(canAskAgain = false)

        sut.onPermissionChecked(granted = true, canAsk = false)

        assertEquals(ConsentUiState.Hidden, sut.uiState.value)
    }

    @Test
    fun onPermissionResult_ShouldRequestNotifications_WhenApi33OrLater() = runTest {
        val sut = consent(sdkInt = 33)
        val effects = effectsOf(sut)
        signedInWithoutPermission(sut)
        sut.onContinue()

        sut.onPermissionResult(fineGranted = true, coarseGranted = true, canAskAgain = false, notificationsGranted = false)

        assertEquals(listOf(ConsentEffect.RequestLocation, ConsentEffect.RequestNotifications), effects)
    }

    @Test
    fun onPermissionResult_ShouldRequestNotifications_WhenLocationIsDeniedOnApi33() = runTest {
        val sut = consent(sdkInt = 33)
        val effects = effectsOf(sut)
        signedInWithoutPermission(sut)
        sut.onContinue()

        sut.onPermissionResult(fineGranted = false, coarseGranted = false, canAskAgain = true, notificationsGranted = false)

        assertEquals(ConsentEffect.RequestNotifications, effects.last())
    }

    @Test
    fun onPermissionResult_ShouldNotRequestNotifications_WhenApi32OrEarlier() = runTest {
        val sut = consent(sdkInt = 32)
        val effects = effectsOf(sut)
        signedInWithoutPermission(sut)
        sut.onContinue()

        sut.onPermissionResult(fineGranted = true, coarseGranted = true, canAskAgain = false, notificationsGranted = false)

        assertEquals(listOf(ConsentEffect.RequestLocation), effects)
    }

    @Test
    fun onPermissionResult_ShouldNotRequestNotifications_WhenAlreadyGranted() = runTest {
        val sut = consent(sdkInt = 34)
        val effects = effectsOf(sut)
        signedInWithoutPermission(sut)
        sut.onContinue()

        sut.allow()

        assertEquals(listOf(ConsentEffect.RequestLocation), effects)
    }

    @Test
    fun requireLocation_ShouldRunAtOnce_WhenPermissionIsGranted() {
        val sut = consent(FakeConsentStore(accepted = true))
        sut.onSessionChanged(signedIn = true)
        sut.onPermissionChecked(granted = true, canAsk = false)
        var runs = 0

        sut.requireLocation { runs++ }

        assertEquals(1, runs)
        assertEquals(ConsentUiState.Hidden, sut.uiState.value)
    }

    @Test
    fun requireLocation_ShouldShowDisclosureAndRunAfterGrant_WhenPermissionIsMissing() {
        val sut = consent()
        signedInWithoutPermission(sut)
        sut.onNotNow()
        var runs = 0

        sut.requireLocation { runs++ }
        assertEquals(ConsentUiState.Disclosure, sut.uiState.value)
        assertEquals(0, runs)

        sut.onContinue()
        sut.allow()

        assertEquals(1, runs)
        assertEquals(ConsentUiState.Hidden, sut.uiState.value)
    }

    @Test
    fun requireLocation_ShouldRunAfterGrantInSettings_WhenSettingsVariantIsShown() {
        val sut = consent()
        signedInWithoutPermission(sut)
        sut.onContinue()
        sut.deny(canAskAgain = false)
        var runs = 0

        sut.requireLocation { runs++ }
        assertEquals(ConsentUiState.OpenSettings, sut.uiState.value)
        sut.onPermissionChecked(granted = true, canAsk = false)

        assertEquals(1, runs)
    }

    @Test
    fun requireLocation_ShouldDropTheAction_WhenPlayerTapsNotNow() {
        val sut = consent()
        signedInWithoutPermission(sut)
        var runs = 0
        sut.requireLocation { runs++ }

        sut.onNotNow()
        sut.requireLocation { }
        sut.onContinue()
        sut.allow()

        assertEquals(0, runs)
    }

    @Test
    fun requireLocation_ShouldRunOnDeclined_WhenPlayerTapsNotNowOrDenies() {
        val sut = consent()
        signedInWithoutPermission(sut)
        var declined = 0
        var runs = 0

        sut.requireLocation(onDeclined = { declined++ }) { runs++ }
        sut.onNotNow()
        sut.requireLocation(onDeclined = { declined++ }) { runs++ }
        sut.onContinue()
        sut.deny(canAskAgain = true)

        assertEquals(2, declined)
        assertEquals(0, runs)
    }

    @Test
    fun requireLocation_ShouldNotRunOnDeclined_WhenGrantedOrSignedOut() {
        val sut = consent()
        signedInWithoutPermission(sut)
        var declined = 0

        sut.requireLocation(onDeclined = { declined++ }) { }
        sut.onContinue()
        sut.allow()
        sut.onPermissionChecked(granted = false, canAsk = true)
        sut.requireLocation(onDeclined = { declined++ }) { }
        sut.onSessionChanged(signedIn = false)

        assertEquals(0, declined)
    }

    @Test
    fun requireLocation_ShouldDropTheAction_WhenPromptIsDenied() {
        val sut = consent()
        signedInWithoutPermission(sut)
        var runs = 0
        sut.requireLocation { runs++ }

        sut.onContinue()
        sut.deny(canAskAgain = true)
        sut.onPermissionChecked(granted = true, canAsk = false)

        assertEquals(0, runs)
        assertEquals(ConsentUiState.Hidden, sut.uiState.value)
    }

    // --- the main menu asks once per device ---

    @Test
    fun uiState_ShouldRecordTheMenuPrompt_WhenShownAutomatically() {
        val sut = consent()

        signedInWithoutPermission(sut)

        assertEquals(ConsentUiState.Disclosure, sut.uiState.value)
        assertTrue(store.menuPromptShown.value)
    }

    @Test
    fun uiState_ShouldStayHidden_WhenNotOnTheMainMenu() {
        val sut = consent()

        sut.onSessionChanged(signedIn = true)
        sut.onMainMenuVisible(false)
        sut.onPermissionChecked(granted = false, canAsk = true)

        assertEquals(ConsentUiState.Hidden, sut.uiState.value)
        assertFalse("Not shown, so not used up", store.menuPromptShown.value)
    }

    @Test
    fun uiState_ShouldShowDisclosure_WhenReturningToTheMainMenuBeforeItAsked() {
        val sut = consent()
        sut.onSessionChanged(signedIn = true)
        sut.onPermissionChecked(granted = false, canAsk = true)

        sut.onMainMenuVisible(true)

        assertEquals(ConsentUiState.Disclosure, sut.uiState.value)
    }

    @Test
    fun uiState_ShouldStayHidden_WhenTheMenuAlreadyAskedOnThisDevice() {
        val sut = consent(FakeConsentStore(menuPromptShown = true))

        signedInWithoutPermission(sut)

        assertEquals(ConsentUiState.Hidden, sut.uiState.value)
    }

    @Test
    fun uiState_ShouldNotUseUpTheMenuPrompt_WhenPermissionIsAlreadyGranted() {
        val sut = consent()

        sut.onSessionChanged(signedIn = true)
        sut.onMainMenuVisible(true)
        sut.onPermissionChecked(granted = true, canAsk = false)

        assertEquals(ConsentUiState.Hidden, sut.uiState.value)
        assertFalse(store.menuPromptShown.value)
    }

    @Test
    fun uiState_ShouldStayHidden_WhenLeavingAndReturningToTheMenuAfterNotNow() {
        val sut = consent()
        signedInWithoutPermission(sut)
        sut.onNotNow()

        sut.onMainMenuVisible(false)
        sut.onMainMenuVisible(true)
        sut.onPermissionChecked(granted = false, canAsk = true)

        assertEquals(ConsentUiState.Hidden, sut.uiState.value)
    }

    @Test
    fun requireLocation_ShouldShowDisclosure_WhenTheMenuAlreadyAsked() {
        val sut = consent(FakeConsentStore(menuPromptShown = true))
        signedInWithoutPermission(sut)
        var runs = 0

        sut.requireLocation { runs++ }

        assertEquals(ConsentUiState.Disclosure, sut.uiState.value)
        assertEquals(0, runs)
    }

    @Test
    fun requireLocation_ShouldShowOpenSettings_WhenAcceptedInSettingsWithOnlyApproximateLocation() {
        val store = FakeConsentStore(menuPromptShown = true)
        val sut = consent(store)
        sut.onSessionChanged(signedIn = true)
        sut.onPermissionChecked(granted = false, canAsk = false, approximate = true)

        // The Settings screen records the acceptance before its own request.
        store.disclosureAccepted.value = true
        sut.requireLocation { }

        assertEquals(ConsentUiState.OpenSettings, sut.uiState.value)
    }
}
