package nl.hexmaster.theprey.auth

import nl.hexmaster.theprey.data.user.UserDto

/** The signed-in player's game settings on the server: callsign and preferred language. */
interface ProfileSettings {
    /**
     * `PUT /users/settings`. On success the returned profile is stored on the device and published
     * as the session's profile, so every screen shows the new callsign at once.
     */
    suspend fun updateSettings(callsign: String, language: String): SettingsResult
}

sealed interface SettingsResult {
    data class Saved(val profile: UserDto) : SettingsResult

    /**
     * The server rejected the values (400). [field] is the rejected field (`callsign` or
     * `preferredLanguage`) when the server named one; [message] is its first (English) message,
     * for logs only: screens show their own translated text.
     */
    data class Invalid(val message: String, val field: String? = null) : SettingsResult

    /** Network error, another status, or not signed in; the request can be retried. */
    data object Failed : SettingsResult
}
