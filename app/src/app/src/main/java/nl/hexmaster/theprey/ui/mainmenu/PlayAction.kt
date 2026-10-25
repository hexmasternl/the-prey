package nl.hexmaster.theprey.ui.mainmenu

import nl.hexmaster.theprey.data.location.LocationAccess

/** Where the main menu's check for the player's active game stands. */
sealed interface ActiveGameCheck {
    data object Loading : ActiveGameCheck
    data object None : ActiveGameCheck
    data class Active(val gameId: String) : ActiveGameCheck
    data object Failed : ActiveGameCheck
}

enum class PlayActionLabel { PlayNow, ResumeGame }

enum class PlayActionStyle { Signal, Hunter }

/** What the player has to fix before the play button works; shown under the button. */
enum class PlayActionError { LocationMissing, BackgroundLocationMissing, CheckFailed }

/**
 * The main menu's play button. [gameId] is set for [PlayActionLabel.ResumeGame], the game that
 * tapping it opens.
 */
data class PlayActionState(
    val label: PlayActionLabel = PlayActionLabel.PlayNow,
    val style: PlayActionStyle = PlayActionStyle.Signal,
    val enabled: Boolean = false,
    val error: PlayActionError? = null,
    val gameId: String? = null,
)

/**
 * Turns the permissions and the active-game check into the play button. The button works only
 * with full location access and a completed check; a permission error wins over a failed check,
 * because fixing the settings comes first and the check runs again on return anyway.
 */
fun playAction(access: LocationAccess, check: ActiveGameCheck): PlayActionState {
    val permissionError = when (access) {
        // Approximate only is not enough to play: the game needs precise positions.
        LocationAccess.Missing, LocationAccess.ApproximateOnly -> PlayActionError.LocationMissing
        LocationAccess.ForegroundOnly -> PlayActionError.BackgroundLocationMissing
        LocationAccess.Granted -> null
    }
    val active = check as? ActiveGameCheck.Active
    val enabled = permissionError == null && (check == ActiveGameCheck.None || active != null)
    return PlayActionState(
        label = if (active != null) PlayActionLabel.ResumeGame else PlayActionLabel.PlayNow,
        style = if (active != null) PlayActionStyle.Hunter else PlayActionStyle.Signal,
        enabled = enabled,
        error = permissionError ?: PlayActionError.CheckFailed.takeIf { check == ActiveGameCheck.Failed },
        gameId = active?.gameId,
    )
}
