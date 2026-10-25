package nl.hexmaster.theprey.ui.games.create

import androidx.lifecycle.SavedStateHandle
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import kotlinx.coroutines.channels.Channel
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.receiveAsFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch
import nl.hexmaster.theprey.data.game.CreateGameResult
import nl.hexmaster.theprey.data.game.GameRepository
import nl.hexmaster.theprey.data.game.NewGame
import nl.hexmaster.theprey.data.user.ProfileStore
import nl.hexmaster.theprey.ui.playfields.select.PlayFieldSelection

data class CreateGameUiState(
    val playField: PlayFieldSelection? = null,
    val settings: GameSettings = GameSettings(),
    /** The create request is in flight: the form is locked. */
    val submitting: Boolean = false,
    /** The last create request failed; shown with a retry action. */
    val failed: Boolean = false,
) {
    val canCreate: Boolean get() = playField != null && !submitting
}

/** What the screen does when it resumes; see [CreateGameViewModel.onResumed]. */
enum class ResumeAction { None, OpenPicker, Leave }

sealed interface CreateGameEvent {
    /** The server created the game: open its lobby. */
    data class Created(val gameId: String) : CreateGameEvent
}

/**
 * The Create game screen: the chosen playfield, the five settings and the create request. The
 * choices survive rotation and process death through [savedStateHandle]; whether a request was in
 * flight does not, because its outcome is unknown after process death.
 */
class CreateGameViewModel(
    private val gameRepository: GameRepository,
    private val profileStore: ProfileStore,
    private val savedStateHandle: SavedStateHandle,
) : ViewModel() {

    private val _uiState = MutableStateFlow(CreateGameUiState(playField = restoredPlayField(), settings = restoredSettings()))
    val uiState: StateFlow<CreateGameUiState> = _uiState.asStateFlow()

    private val _events = Channel<CreateGameEvent>(Channel.BUFFERED)

    /** One-shot events, so the navigation to the lobby happens exactly once. */
    val events: Flow<CreateGameEvent> = _events.receiveAsFlow()

    /** Whether the playfield selection screen was opened by itself once; prevents a reopen loop after Cancel. */
    val pickerLaunched: Boolean get() = savedStateHandle[KEY_PICKER_LAUNCHED] ?: false

    fun onPickerLaunched() {
        savedStateHandle[KEY_PICKER_LAUNCHED] = true
    }

    /** True when the picker opened by itself was cancelled before anything was chosen: leave the screen. */
    val firstPickCancelled: Boolean get() = pickerLaunched && _uiState.value.playField == null

    /**
     * Called each time the screen resumes, with the selection the picker delivered (if any). Opens
     * the picker by itself once while no playfield is chosen, and leaves the screen when that first
     * pick was cancelled.
     */
    fun onResumed(delivered: PlayFieldSelection?): ResumeAction {
        delivered?.let(::onPlayFieldSelected)
        return when {
            firstPickCancelled -> ResumeAction.Leave
            _uiState.value.playField == null -> {
                onPickerLaunched()
                ResumeAction.OpenPicker
            }
            else -> ResumeAction.None
        }
    }

    fun onPlayFieldSelected(selection: PlayFieldSelection) {
        if (_uiState.value.submitting) return
        savedStateHandle[KEY_PLAYFIELD_ID] = selection.id
        savedStateHandle[KEY_PLAYFIELD_NAME] = selection.name
        savedStateHandle[KEY_PLAYFIELD_PUBLIC] = selection.isPublic
        _uiState.update { it.copy(playField = selection) }
    }

    fun onSettingChange(setting: GameSetting, minutes: Int) {
        if (_uiState.value.submitting) return
        val settings = _uiState.value.settings.with(setting, minutes)
        GameSetting.entries.forEach { savedStateHandle[it.key] = settings.valueOf(it) }
        _uiState.update { it.copy(settings = settings) }
    }

    /** Sends the create request; also the retry after a failure. Ignored while one is in flight. */
    fun create() {
        val state = _uiState.value
        val playField = state.playField ?: return
        if (state.submitting) return
        _uiState.update { it.copy(submitting = true, failed = false) }
        viewModelScope.launch {
            val profile = profileStore.read()
            val displayName = profile?.callsign?.takeIf { it.isNotBlank() } ?: profile?.displayName.orEmpty()
            val settings = state.settings
            val result = gameRepository.createGame(
                NewGame(
                    playFieldId = playField.id,
                    displayName = displayName,
                    gameDuration = settings.duration,
                    hunterDelay = settings.hunterDelay,
                    finalStage = settings.finalStage,
                    pingInterval = settings.pingInterval,
                    finalPingInterval = settings.finalPingInterval,
                ),
            )
            when (result) {
                is CreateGameResult.Created -> {
                    _uiState.update { it.copy(submitting = false) }
                    _events.send(CreateGameEvent.Created(result.gameId))
                }
                CreateGameResult.Failed -> _uiState.update { it.copy(submitting = false, failed = true) }
            }
        }
    }

    fun dismissError() {
        _uiState.update { it.copy(failed = false) }
    }

    private fun restoredPlayField(): PlayFieldSelection? {
        val id = savedStateHandle.get<String>(KEY_PLAYFIELD_ID) ?: return null
        return PlayFieldSelection(
            id = id,
            name = savedStateHandle.get<String>(KEY_PLAYFIELD_NAME).orEmpty(),
            isPublic = savedStateHandle.get<Boolean>(KEY_PLAYFIELD_PUBLIC) ?: false,
        )
    }

    private fun restoredSettings(): GameSettings = GameSetting.entries.fold(GameSettings()) { settings, setting ->
        savedStateHandle.get<Int>(setting.key)?.let { settings.with(setting, it) } ?: settings
    }

    internal companion object {
        const val KEY_PICKER_LAUNCHED = "game_create_picker_launched"
        const val KEY_PLAYFIELD_ID = "game_create_playfield_id"
        const val KEY_PLAYFIELD_NAME = "game_create_playfield_name"
        const val KEY_PLAYFIELD_PUBLIC = "game_create_playfield_public"

        val GameSetting.key: String get() = "game_create_setting_$name"
    }
}
