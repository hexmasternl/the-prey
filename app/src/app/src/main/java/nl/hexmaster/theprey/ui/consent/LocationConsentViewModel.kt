package nl.hexmaster.theprey.ui.consent

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import kotlinx.coroutines.channels.Channel
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.receiveAsFlow
import kotlinx.coroutines.launch
import nl.hexmaster.theprey.data.location.LocationConsentStore

/** What the location consent host shows on top of the signed-in content. */
sealed interface ConsentUiState {
    /** Nothing: permission granted, not signed in, already asked on the main menu, or dismissed. */
    data object Hidden : ConsentUiState

    /** The prominent disclosure; Continue leads to the system location prompt. */
    data object Disclosure : ConsentUiState

    /** The system no longer prompts (or only approximate location is allowed): send the player to settings. */
    data object OpenSettings : ConsentUiState

    /** Precise location was just granted "While using the app": guide the player to "Allow all the time" in settings. */
    data object AllTheTime : ConsentUiState
}

/** One-shot Android calls the host makes on the view model's behalf. */
sealed interface ConsentEffect {
    /** Launch the system prompt for fine + coarse location. */
    data object RequestLocation : ConsentEffect

    /** Launch the system prompt for POST_NOTIFICATIONS (API 33+ only). */
    data object RequestNotifications : ConsentEffect

    /** Open this app's page in the system settings. */
    data object OpenAppSettings : ConsentEffect
}

/**
 * Decides when the location tracking disclosure is shown and what is requested next. The host
 * composable feeds it permission checks and prompt results and performs its [effects]; nothing in
 * here touches Android APIs.
 *
 * The disclosure opens by itself only once per device, on the main menu (see
 * [LocationConsentStore.menuPromptShown]); after that only a game action ([requireLocation]) opens
 * it. The Settings screen shows the same disclosure as a panel and runs its own request.
 *
 * Only a fine-location grant counts as [granted]: gameplay needs precise positions. Any grant
 * (fine or coarse) is enough for [canReadLocation], used by the main menu readout.
 *
 * A fine grant without background location inside the flow (API 29+) is followed by the
 * [ConsentUiState.AllTheTime] step; background location is never requested by a prompt.
 *
 * [sdkInt] is `Build.VERSION.SDK_INT`; the notification permission exists from API 33.
 */
class LocationConsentViewModel(
    private val store: LocationConsentStore,
    private val sdkInt: Int,
) : ViewModel() {

    private val _uiState = MutableStateFlow<ConsentUiState>(ConsentUiState.Hidden)
    val uiState: StateFlow<ConsentUiState> = _uiState.asStateFlow()

    private val _granted = MutableStateFlow(false)

    /** Precise location is granted: start/join game actions may continue. */
    val granted: StateFlow<Boolean> = _granted.asStateFlow()

    private val _canReadLocation = MutableStateFlow(false)

    /** Fine or coarse location is granted: enough for a position readout, not for gameplay. */
    val canReadLocation: StateFlow<Boolean> = _canReadLocation.asStateFlow()

    private val _effects = Channel<ConsentEffect>(Channel.BUFFERED)
    val effects: Flow<ConsentEffect> = _effects.receiveAsFlow()

    private var signedIn = false

    /** Null until read from the store; the disclosure waits for it. */
    private var accepted: Boolean? = null

    /** False until the host reported the permission state once. */
    private var checked = false
    private var approximateOnly = false

    /** The last request was refused without a system prompt: only the settings page can grant it now. */
    private var blocked = false

    /** Null until read from the store; the main menu shows the disclosure by itself only while false. */
    private var menuPromptShown: Boolean? = null

    /** The main menu is the current screen: the only place the disclosure may appear by itself. */
    private var onMainMenu = false

    /**
     * The disclosure flow is running: opened once automatically on the main menu, or by a game
     * action ([requireLocation]). Closed by Not now, a plain denial, a grant or signing out.
     */
    private var open = false

    /** A system prompt is open; lifecycle checks while it is up must not re-show the dialog. */
    private var awaitingResult = false

    /** The flow granted precise location without background: the "Allow all the time" step is showing. */
    private var allTheTimeStep = false

    /** The step sent the player to the settings page; the next resume is their return from it. */
    private var sentToSettings = false

    /** A game action waiting for a grant (see [requireLocation]). */
    private var pendingAction: (() -> Unit)? = null

    /** Runs instead of [pendingAction] when the player declines. */
    private var pendingDeclined: (() -> Unit)? = null

    init {
        viewModelScope.launch {
            menuPromptShown = store.isMenuPromptShown()
            // Followed, not read once: the Settings screen records acceptance too.
            store.disclosureAccepted.collect {
                accepted = it
                refresh()
            }
        }
    }

    /** The session switched; the disclosure is only shown to signed-in players. */
    fun onSessionChanged(signedIn: Boolean) {
        if (!signedIn) {
            pendingAction = null
            pendingDeclined = null
            open = false
            closeAllTheTimeStep()
        }
        this.signedIn = signedIn
        refresh()
    }

    /** Whether the main menu is the current screen; the automatic first-time disclosure waits for it. */
    fun onMainMenuVisible(visible: Boolean) {
        onMainMenu = visible
        refresh()
    }

    /**
     * The current permission state, checked on start and on every resume.
     * [granted] is the fine-location grant, [canAsk] is `shouldShowRequestPermissionRationale`
     * for fine location, [approximate] is the coarse-location grant, [backgroundGranted] is
     * "Allow all the time" (always true below API 29).
     */
    fun onPermissionChecked(
        granted: Boolean,
        canAsk: Boolean,
        approximate: Boolean = false,
        backgroundGranted: Boolean = true,
    ) {
        checked = true
        updateGrant(fine = granted, coarse = approximate)
        // The system would show its prompt again, so the settings detour is no longer needed.
        if (canAsk) blocked = false
        if (granted) runPendingAction()
        // Resumes from the prompts closing keep the step; the return from settings ends it either way.
        if (allTheTimeStep && (backgroundGranted || sentToSettings)) {
            closeAllTheTimeStep()
            open = false
        }
        refresh()
    }

    /** Continue in the disclosure: record acceptance and ask for location. */
    fun onContinue() {
        if (_uiState.value != ConsentUiState.Disclosure) return
        accepted = true
        viewModelScope.launch { store.setDisclosureAccepted(true) }
        awaitingResult = true
        _uiState.value = ConsentUiState.Hidden
        _effects.trySend(ConsentEffect.RequestLocation)
    }

    /** Not now (either variant): close without asking. Only a game action opens it again. */
    fun onNotNow() {
        open = false
        closeAllTheTimeStep()
        declinePendingAction()
        refresh()
    }

    /** The settings and "all the time" variants' button. The dialog stays; the resume check decides what comes next. */
    fun onOpenSettings() {
        if (allTheTimeStep) sentToSettings = true
        _effects.trySend(ConsentEffect.OpenAppSettings)
    }

    /**
     * The answer to the location prompt. [canAskAgain] is `shouldShowRequestPermissionRationale`
     * for fine location right after the answer: false after a denial means the system will not
     * prompt again. [notificationsGranted] is whether POST_NOTIFICATIONS is already granted.
     * [backgroundGranted] is "Allow all the time" (always true below API 29).
     */
    fun onPermissionResult(
        fineGranted: Boolean,
        coarseGranted: Boolean,
        canAskAgain: Boolean,
        notificationsGranted: Boolean,
        backgroundGranted: Boolean = true,
    ) {
        awaitingResult = false
        checked = true
        updateGrant(fine = fineGranted, coarse = coarseGranted)
        if (fineGranted) {
            blocked = false
            // The game action runs on the precise grant; the step only guides the player further.
            runPendingAction()
            if (open && !backgroundGranted && sdkInt >= BACKGROUND_LOCATION_SDK) allTheTimeStep = true
        } else {
            declinePendingAction()
            blocked = !canAskAgain
            // A first denial closes the flow like Not now; a blocked or approximate-only answer
            // shows the settings variant instead.
            if (!blocked && !approximateOnly) open = false
        }
        if (sdkInt >= NOTIFICATIONS_SDK && !notificationsGranted) {
            _effects.trySend(ConsentEffect.RequestNotifications)
        }
        refresh()
    }

    /**
     * Runs [onGranted] once precise location is granted. Already granted: runs at once. Otherwise
     * the disclosure is shown and [onGranted] runs after a grant; it is dropped when the player
     * declines, and [onDeclined] runs instead.
     */
    fun requireLocation(onDeclined: () -> Unit = {}, onGranted: () -> Unit) {
        if (_granted.value) {
            onGranted()
            return
        }
        pendingAction = onGranted
        pendingDeclined = onDeclined
        open = true
        refresh()
    }

    private fun updateGrant(fine: Boolean, coarse: Boolean) {
        _granted.value = fine
        _canReadLocation.value = fine || coarse
        approximateOnly = coarse && !fine
    }

    private fun closeAllTheTimeStep() {
        allTheTimeStep = false
        sentToSettings = false
    }

    private fun runPendingAction() {
        val action = pendingAction ?: return
        pendingAction = null
        pendingDeclined = null
        action()
    }

    private fun declinePendingAction() {
        val declined = pendingDeclined
        pendingAction = null
        pendingDeclined = null
        declined?.invoke()
    }

    private fun refresh() {
        if (awaitingResult) return
        val accepted = accepted
        if (!signedIn || !checked || accepted == null || menuPromptShown == null) {
            _uiState.value = ConsentUiState.Hidden
            return
        }
        if (_granted.value && !allTheTimeStep) open = false
        // The main menu asks by itself once per device, whatever the player then picks.
        if (!open && !_granted.value && onMainMenu && menuPromptShown == false) {
            open = true
            menuPromptShown = true
            viewModelScope.launch { store.setMenuPromptShown() }
        }
        _uiState.value = when {
            !open -> ConsentUiState.Hidden
            allTheTimeStep -> ConsentUiState.AllTheTime
            // Approximate-only after going through this flow: asking again does not help, settings does.
            accepted && (blocked || approximateOnly) -> ConsentUiState.OpenSettings
            else -> ConsentUiState.Disclosure
        }
    }

    private companion object {
        /** Build.VERSION_CODES.TIRAMISU: POST_NOTIFICATIONS is a runtime permission from here on. */
        const val NOTIFICATIONS_SDK = 33

        /** Build.VERSION_CODES.Q: background location is a separate permission from here on. */
        const val BACKGROUND_LOCATION_SDK = 29
    }
}
