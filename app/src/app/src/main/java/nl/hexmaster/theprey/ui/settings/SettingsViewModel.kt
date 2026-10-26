package nl.hexmaster.theprey.ui.settings

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.Job
import kotlinx.coroutines.channels.Channel
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.receiveAsFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch
import nl.hexmaster.theprey.auth.ProfileSettings
import nl.hexmaster.theprey.auth.SessionController
import nl.hexmaster.theprey.auth.SessionState
import nl.hexmaster.theprey.auth.SettingsResult
import nl.hexmaster.theprey.data.display.DisplayPreferencesStore
import nl.hexmaster.theprey.data.hints.HintsStore
import nl.hexmaster.theprey.data.language.AppLanguage
import nl.hexmaster.theprey.data.language.LanguageChoice
import nl.hexmaster.theprey.data.user.UserDto

/** The outcome of "Show hints again". */
enum class HintsResetMessage { Done, Failed }

/** The outcome of the last callsign save, shown in the Callsign section until the next edit. */
sealed interface CallsignSaveStatus {
    data object Idle : CallsignSaveStatus
    data object Saved : CallsignSaveStatus

    /** The server refused the callsign (400). */
    data object Rejected : CallsignSaveStatus

    /** Network or server error; Save can be tapped again. */
    data object Failed : CallsignSaveStatus
}

data class SettingsUiState(
    /** The callsign field's content (the draft, untrimmed). */
    val callsign: String = "",
    /** The stored callsign; empty when the player has none yet. */
    val currentCallsign: String = "",
    /** Shown in the hint while the player has no callsign. */
    val displayName: String = "",
    val check: CallsignCheck = CallsignCheck.Unchanged,
    val saving: Boolean = false,
    val saveStatus: CallsignSaveStatus = CallsignSaveStatus.Idle,
    val language: LanguageChoice = LanguageChoice.System,
    /** The language changed on this phone but could not be sent to the server yet. */
    val languageSyncPending: Boolean = false,
    /** The game screen keeps the display awake; follows the stored value. */
    val keepScreenOn: Boolean = false,
) {
    val canSave: Boolean get() = check is CallsignCheck.Valid && !saving
}

/**
 * Callsign and language settings. The callsign is checked as the server does and saved with
 * `PUT /users/settings`; a language change applies on the phone first and is then synced as the
 * player's preferred language, best effort (see design Decision 4). [resetHints] brings the
 * first-time tours back; the keep-screen-on choice stays on the phone.
 */
class SettingsViewModel(
    private val session: SessionController,
    private val profileSettings: ProfileSettings,
    private val appLanguage: AppLanguage,
    private val hints: HintsStore,
    private val displayPreferences: DisplayPreferencesStore,
) : ViewModel() {

    private val _uiState: MutableStateFlow<SettingsUiState>
    val uiState: StateFlow<SettingsUiState>

    private var languageSync: Job? = null

    private val hintsMessages = Channel<HintsResetMessage>(Channel.BUFFERED)

    /** One-shot outcome of [resetHints], for a snackbar. */
    val hintsResetMessages: Flow<HintsResetMessage> = hintsMessages.receiveAsFlow()

    init {
        val profile = profile()
        _uiState = MutableStateFlow(
            SettingsUiState(
                callsign = profile?.callsign.orEmpty(),
                currentCallsign = profile?.callsign.orEmpty(),
                displayName = profile?.displayName.orEmpty(),
                language = appLanguage.current(),
            ),
        )
        uiState = _uiState.asStateFlow()

        viewModelScope.launch {
            session.state.collect { state ->
                val stored = (state as? SessionState.SignedIn)?.profile ?: return@collect
                _uiState.update {
                    it.copy(
                        currentCallsign = stored.callsign,
                        displayName = stored.displayName,
                        check = validateCallsign(it.callsign, stored.callsign),
                    )
                }
            }
        }
        viewModelScope.launch {
            displayPreferences.keepScreenOnInGame.collect { enabled ->
                _uiState.update { it.copy(keepScreenOn = enabled) }
            }
        }
        // Reconcile a change the server missed: offline, or made in Android 13's system screen.
        syncLanguage(reportFailure = false)
    }

    fun onCallsignChange(value: String) {
        _uiState.update {
            it.copy(
                callsign = value,
                check = validateCallsign(value, it.currentCallsign),
                saveStatus = CallsignSaveStatus.Idle,
            )
        }
    }

    fun onSave() {
        val state = _uiState.value
        val check = state.check as? CallsignCheck.Valid ?: return
        if (state.saving) return
        _uiState.update { it.copy(saving = true, saveStatus = CallsignSaveStatus.Idle) }
        viewModelScope.launch {
            // Always sends the effective language too, which also syncs a pending language change.
            val result = profileSettings.updateSettings(check.callsign, appLanguage.effective())
            _uiState.update {
                when (result) {
                    is SettingsResult.Saved -> it.copy(
                        saving = false,
                        callsign = result.profile.callsign,
                        currentCallsign = result.profile.callsign,
                        check = CallsignCheck.Unchanged,
                        saveStatus = CallsignSaveStatus.Saved,
                        languageSyncPending = false,
                    )
                    is SettingsResult.Invalid -> it.copy(saving = false, saveStatus = CallsignSaveStatus.Rejected)
                    SettingsResult.Failed -> it.copy(saving = false, saveStatus = CallsignSaveStatus.Failed)
                }
            }
        }
    }

    /** Applies [choice] to the app at once; the server sync never blocks or undoes it. */
    fun onLanguageSelected(choice: LanguageChoice) {
        if (choice == _uiState.value.language) return
        appLanguage.set(choice)
        _uiState.update { it.copy(language = choice, languageSyncPending = false) }
        syncLanguage(reportFailure = true)
    }

    /**
     * Sends the effective language with the stored callsign when it differs from the profile.
     * Skipped while the player has no valid callsign: the server needs one, and the next callsign
     * save sends the language anyway.
     */
    private fun syncLanguage(reportFailure: Boolean) {
        val profile = profile() ?: return
        val language = appLanguage.effective()
        if (language == profile.preferredLanguage || !isValidCallsign(profile.callsign)) return
        languageSync?.cancel()
        languageSync = viewModelScope.launch {
            val result = profileSettings.updateSettings(profile.callsign, language)
            _uiState.update {
                when {
                    result is SettingsResult.Saved -> it.copy(languageSyncPending = false)
                    reportFailure -> it.copy(languageSyncPending = true)
                    else -> it
                }
            }
        }
    }

    /** Stores the choice; the switch follows the stored value, so a failed write leaves it as it was. */
    fun onKeepScreenOnChange(enabled: Boolean) {
        viewModelScope.launch { displayPreferences.setKeepScreenOnInGame(enabled) }
    }

    /** Marks every first-time tour not seen, so each shows again the next time its situation occurs. */
    fun resetHints() {
        viewModelScope.launch {
            val message = try {
                hints.resetAll()
                HintsResetMessage.Done
            } catch (e: CancellationException) {
                throw e
            } catch (_: Exception) {
                HintsResetMessage.Failed
            }
            hintsMessages.send(message)
        }
    }

    private fun profile(): UserDto? = (session.state.value as? SessionState.SignedIn)?.profile
}
