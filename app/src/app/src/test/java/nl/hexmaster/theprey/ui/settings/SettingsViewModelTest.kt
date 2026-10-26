package nl.hexmaster.theprey.ui.settings

import kotlinx.coroutines.CompletableDeferred
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.map
import kotlinx.coroutines.flow.toList
import kotlinx.coroutines.launch
import kotlinx.coroutines.test.UnconfinedTestDispatcher
import kotlinx.coroutines.test.resetMain
import kotlinx.coroutines.test.runTest
import kotlinx.coroutines.test.setMain
import nl.hexmaster.theprey.auth.ProfileSettings
import nl.hexmaster.theprey.auth.SessionState
import nl.hexmaster.theprey.auth.SettingsResult
import nl.hexmaster.theprey.auth.aProfile
import nl.hexmaster.theprey.data.display.DisplayPreferencesStore
import nl.hexmaster.theprey.data.hints.Hint
import nl.hexmaster.theprey.data.hints.HintsStore
import nl.hexmaster.theprey.data.language.AppLanguage
import nl.hexmaster.theprey.data.language.LanguageChoice
import nl.hexmaster.theprey.data.user.UserDto
import nl.hexmaster.theprey.ui.FakeSession
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test
import java.io.IOException

@OptIn(ExperimentalCoroutinesApi::class)
class SettingsViewModelTest {

    /** Records every call; answers with [result], after [gate] completes when it is set. Publishes saved profiles like SessionManager. */
    private class FakeProfileSettings(private val session: FakeSession) : ProfileSettings {
        val calls = mutableListOf<Pair<String, String>>()
        var result: (callsign: String, language: String) -> SettingsResult = { callsign, language ->
            SettingsResult.Saved(currentProfile().copy(callsign = callsign, preferredLanguage = language))
        }
        var gate: CompletableDeferred<Unit>? = null

        fun currentProfile(): UserDto = (session.state.value as SessionState.SignedIn).profile

        override suspend fun updateSettings(callsign: String, language: String): SettingsResult {
            calls += callsign to language
            gate?.await()
            val outcome = result(callsign, language)
            if (outcome is SettingsResult.Saved) session.state.value = SessionState.SignedIn(outcome.profile)
            return outcome
        }
    }

    private class FakeAppLanguage(var choice: LanguageChoice = LanguageChoice.System, private val device: String = "en") :
        AppLanguage {
        override fun current() = choice
        override fun set(choice: LanguageChoice) {
            this.choice = choice
        }
        override fun effective() = choice.tag ?: device
    }

    private var session = FakeSession(SessionState.SignedIn(aProfile(callsign = "NightOwl")))
    private var settings = FakeProfileSettings(session)
    private val language = FakeAppLanguage()

    @Before
    fun setUp() = Dispatchers.setMain(UnconfinedTestDispatcher())

    @After
    fun tearDown() = Dispatchers.resetMain()

    private fun signedInAs(profile: UserDto) {
        session = FakeSession(SessionState.SignedIn(profile))
        settings = FakeProfileSettings(session)
    }

    /** Seen flags in memory; [failReset] makes [resetAll] throw like a failing disk. */
    private class FakeHintsStore(seen: Set<Hint> = emptySet(), var failReset: Boolean = false) : HintsStore {
        val current = MutableStateFlow(seen)

        override fun seen(hint: Hint): Flow<Boolean> = current.map { hint in it }

        override suspend fun markSeen(hint: Hint) {
            current.value = current.value + hint
        }

        override suspend fun resetAll() {
            if (failReset) throw IOException("disk")
            current.value = emptySet()
        }
    }

    private val hints = FakeHintsStore(seen = Hint.entries.toSet())

    /** The stored choice in memory; [failWrite] leaves it unchanged like a failing disk (the real store logs and swallows). */
    private class FakeDisplayPreferencesStore(stored: Boolean = false, var failWrite: Boolean = false) :
        DisplayPreferencesStore {
        val current = MutableStateFlow(stored)
        override val keepScreenOnInGame: Flow<Boolean> = current

        override suspend fun setKeepScreenOnInGame(enabled: Boolean) {
            if (!failWrite) current.value = enabled
        }
    }

    private val display = FakeDisplayPreferencesStore()

    private fun viewModel() = SettingsViewModel(session, settings, language, hints, display)

    @Test
    fun uiState_ShouldShowTheCurrentCallsign_WhenOpened() {
        val sut = viewModel()

        assertEquals("NightOwl", sut.uiState.value.callsign)
        assertEquals(CallsignCheck.Unchanged, sut.uiState.value.check)
        assertFalse(sut.uiState.value.canSave)
    }

    @Test
    fun uiState_ShouldHaveAnEmptyField_WhenThePlayerHasNoCallsign() {
        signedInAs(aProfile(callsign = ""))

        val sut = viewModel()

        assertEquals("", sut.uiState.value.callsign)
        assertEquals("", sut.uiState.value.currentCallsign)
        assertEquals("Nina Hawk", sut.uiState.value.displayName)
    }

    @Test
    fun onCallsignChange_ShouldDisableSave_WhenTheValueBreaksTheRule() {
        val sut = viewModel()

        sut.onCallsignChange("Hunter!")

        assertEquals(CallsignCheck.Invalid, sut.uiState.value.check)
        assertFalse(sut.uiState.value.canSave)
    }

    @Test
    fun onSave_ShouldSendTrimmedCallsignWithEffectiveLanguage_WhenValid() {
        language.choice = LanguageChoice.Dutch
        signedInAs(aProfile(callsign = "NightOwl").copy(preferredLanguage = "nl"))
        val sut = viewModel()

        sut.onCallsignChange("  Night Owl #7  ")
        sut.onSave()

        assertEquals(listOf("Night Owl #7" to "nl"), settings.calls)
        val state = sut.uiState.value
        assertEquals(CallsignSaveStatus.Saved, state.saveStatus)
        assertEquals("Night Owl #7", state.callsign)
        assertEquals("Night Owl #7", state.currentCallsign)
        assertFalse(state.saving)
        assertFalse("The saved value is the new baseline", state.canSave)
    }

    @Test
    fun onSave_ShouldShowRejectionAndKeepTheDraft_WhenServerRejects() {
        settings.result = { _, _ -> SettingsResult.Invalid("A callsign must be 3-30 characters.", field = "callsign") }
        val sut = viewModel()

        sut.onCallsignChange("Owl 777")
        sut.onSave()

        val state = sut.uiState.value
        assertEquals(CallsignSaveStatus.Rejected, state.saveStatus)
        assertEquals("Owl 777", state.callsign)
        assertEquals("NightOwl", state.currentCallsign)
        assertFalse(state.saving)
    }

    @Test
    fun onCallsignChange_ShouldClearTheLastSaveStatus_WhenEditedAgain() {
        settings.result = { _, _ -> SettingsResult.Failed }
        val sut = viewModel()
        sut.onCallsignChange("Night Owl")
        sut.onSave()

        sut.onCallsignChange("Night Owl 2")

        assertEquals(CallsignSaveStatus.Idle, sut.uiState.value.saveStatus)
    }

    @Test
    fun onSave_ShouldBeRetryable_WhenTheRequestFails() {
        settings.result = { _, _ -> SettingsResult.Failed }
        val sut = viewModel()
        sut.onCallsignChange("Night Owl")
        sut.onSave()

        assertEquals(CallsignSaveStatus.Failed, sut.uiState.value.saveStatus)
        assertEquals("Night Owl", sut.uiState.value.callsign)
        assertTrue(sut.uiState.value.canSave)

        settings.result = { callsign, lang -> SettingsResult.Saved(aProfile(callsign = callsign).copy(preferredLanguage = lang)) }
        sut.onSave()

        assertEquals(CallsignSaveStatus.Saved, sut.uiState.value.saveStatus)
        assertEquals(2, settings.calls.size)
    }

    @Test
    fun onSave_ShouldNotSendTwice_WhileASaveIsInProgress() {
        val gate = CompletableDeferred<Unit>()
        settings.gate = gate
        val sut = viewModel()
        sut.onCallsignChange("Night Owl")

        sut.onSave()
        assertTrue(sut.uiState.value.saving)
        assertFalse(sut.uiState.value.canSave)
        sut.onSave()
        gate.complete(Unit)

        assertEquals(1, settings.calls.size)
        assertFalse(sut.uiState.value.saving)
    }

    @Test
    fun onSave_ShouldDoNothing_WhenTheCallsignIsUnchanged() {
        val sut = viewModel()

        sut.onSave()

        assertTrue(settings.calls.isEmpty())
    }

    @Test
    fun onLanguageSelected_ShouldApplyLocallyAndSync_WhenOnline() {
        val sut = viewModel()

        sut.onLanguageSelected(LanguageChoice.Dutch)

        assertEquals(LanguageChoice.Dutch, language.choice)
        assertEquals(LanguageChoice.Dutch, sut.uiState.value.language)
        assertEquals(listOf("NightOwl" to "nl"), settings.calls)
        assertFalse(sut.uiState.value.languageSyncPending)
    }

    @Test
    fun onLanguageSelected_ShouldKeepTheLanguageAndShowTheNotice_WhenOffline() {
        settings.result = { _, _ -> SettingsResult.Failed }
        val sut = viewModel()

        sut.onLanguageSelected(LanguageChoice.Dutch)

        assertEquals(LanguageChoice.Dutch, language.choice)
        assertEquals(LanguageChoice.Dutch, sut.uiState.value.language)
        assertTrue(sut.uiState.value.languageSyncPending)
    }

    @Test
    fun onSave_ShouldSendThePendingLanguage_WhenSavedAfterAnOfflineLanguageChange() {
        settings.result = { _, _ -> SettingsResult.Failed }
        val sut = viewModel()
        sut.onLanguageSelected(LanguageChoice.Dutch)
        settings.result = { callsign, lang -> SettingsResult.Saved(aProfile(callsign = callsign).copy(preferredLanguage = lang)) }

        sut.onCallsignChange("Night Owl")
        sut.onSave()

        assertEquals("Night Owl" to "nl", settings.calls.last())
        assertFalse(sut.uiState.value.languageSyncPending)
    }

    @Test
    fun onLanguageSelected_ShouldOnlyApplyLocally_WhenThePlayerHasNoCallsign() {
        signedInAs(aProfile(callsign = ""))
        val sut = viewModel()

        sut.onLanguageSelected(LanguageChoice.Dutch)

        assertEquals(LanguageChoice.Dutch, language.choice)
        assertTrue(settings.calls.isEmpty())
        assertFalse(sut.uiState.value.languageSyncPending)
    }

    @Test
    fun onLanguageSelected_ShouldNotSync_WhenTheEffectiveLanguageIsAlreadyStored() {
        val sut = viewModel()

        // The profile already has "en".
        sut.onLanguageSelected(LanguageChoice.English)

        assertTrue(settings.calls.isEmpty())
    }

    @Test
    fun init_ShouldSyncTheLanguage_WhenTheProfileHasAnotherLanguage() {
        language.choice = LanguageChoice.Dutch

        viewModel()

        assertEquals(listOf("NightOwl" to "nl"), settings.calls)
    }

    @Test
    fun init_ShouldNotSync_WhenTheProfileLanguageMatches() {
        viewModel()

        assertTrue(settings.calls.isEmpty())
    }

    @Test
    fun init_ShouldStayQuiet_WhenTheOpenTimeSyncFails() {
        language.choice = LanguageChoice.Dutch
        settings.result = { _, _ -> SettingsResult.Failed }

        val sut = viewModel()

        assertEquals(1, settings.calls.size)
        assertFalse(sut.uiState.value.languageSyncPending)
    }

    // ---- First-time hints ----

    @Test
    fun resetHints_ShouldMarkEveryTourUnseenAndConfirm_WhenStorageWorks() = runTest {
        val sut = viewModel()
        val messages = mutableListOf<HintsResetMessage>()
        backgroundScope.launch(UnconfinedTestDispatcher(testScheduler)) { sut.hintsResetMessages.toList(messages) }

        sut.resetHints()

        assertTrue(hints.current.value.isEmpty())
        assertEquals(listOf(HintsResetMessage.Done), messages)
    }

    @Test
    fun resetHints_ShouldReportTheFailure_WhenStorageFails() = runTest {
        hints.failReset = true
        val sut = viewModel()
        val messages = mutableListOf<HintsResetMessage>()
        backgroundScope.launch(UnconfinedTestDispatcher(testScheduler)) { sut.hintsResetMessages.toList(messages) }

        sut.resetHints()

        assertEquals(Hint.entries.toSet(), hints.current.value)
        assertEquals(listOf(HintsResetMessage.Failed), messages)
    }

    // ---- Keep screen on ----

    @Test
    fun uiState_ShouldShowKeepScreenOnOff_WhenNothingIsStored() {
        val sut = viewModel()

        assertFalse(sut.uiState.value.keepScreenOn)
    }

    @Test
    fun uiState_ShouldShowTheStoredKeepScreenOn_WhenOpened() {
        display.current.value = true

        val sut = viewModel()

        assertTrue(sut.uiState.value.keepScreenOn)
    }

    @Test
    fun onKeepScreenOnChange_ShouldStoreTheChoice_WhenToggled() {
        val sut = viewModel()

        sut.onKeepScreenOnChange(true)

        assertTrue(display.current.value)
        assertTrue(sut.uiState.value.keepScreenOn)

        sut.onKeepScreenOnChange(false)

        assertFalse(display.current.value)
        assertFalse(sut.uiState.value.keepScreenOn)
    }

    @Test
    fun uiState_ShouldFollowTheStore_WhenTheStoredValueChanges() {
        val sut = viewModel()

        display.current.value = true

        assertTrue(sut.uiState.value.keepScreenOn)
    }

    @Test
    fun onKeepScreenOnChange_ShouldKeepTheStoredValue_WhenTheWriteFails() {
        display.failWrite = true
        val sut = viewModel()

        sut.onKeepScreenOnChange(true)

        assertFalse(sut.uiState.value.keepScreenOn)
    }
}
