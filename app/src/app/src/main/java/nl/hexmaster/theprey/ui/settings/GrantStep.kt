package nl.hexmaster.theprey.ui.settings

import nl.hexmaster.theprey.data.location.LocationAccess

/** What the Settings screen's Grant location access action does next. */
enum class GrantStep {
    /** Show the system prompt for precise location (fine + coarse). */
    RequestPrecise,

    /** The system no longer prompts: send the player to the app's settings page for precise location. */
    OpenSettingsForPrecise,

    /** Precise "While using the app" is there; "Allow all the time" is only offered in the settings page. */
    OpenSettingsForAllTheTime,

    /** Everything a game needs is granted: no action. */
    None,
}

/**
 * Whether the system would still show the fine-location prompt. Before the first request through
 * the disclosure ([disclosureAccepted] false) it always does; after one, a false
 * `shouldShowRequestPermissionRationale` ([rationale]) means it was denied for good.
 */
fun canAskFine(rationale: Boolean, disclosureAccepted: Boolean): Boolean = rationale || !disclosureAccepted

/** The next step for [access]; background location is never requested at runtime. */
fun grantStep(access: LocationAccess, canAskFine: Boolean): GrantStep = when (access) {
    LocationAccess.Missing, LocationAccess.ApproximateOnly ->
        if (canAskFine) GrantStep.RequestPrecise else GrantStep.OpenSettingsForPrecise
    LocationAccess.ForegroundOnly -> GrantStep.OpenSettingsForAllTheTime
    LocationAccess.Granted -> GrantStep.None
}
