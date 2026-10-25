package nl.hexmaster.theprey.data.user

import kotlinx.serialization.Serializable

/** The signed-in player's record, as returned by the Users API (`UserDto` on the server). */
@Serializable
data class UserDto(
    val userId: String,
    val displayName: String,
    val callsign: String = "",
    val emailAddress: String,
    val preferredLanguage: String,
)

/** Body of `POST /users`, which registers the caller or returns their existing record. */
@Serializable
data class CreateUserRequest(
    val firstName: String?,
    val lastName: String?,
    val emailAddress: String,
    val isEmailVerified: Boolean,
    val preferredLanguage: String?,
)

/**
 * Body of `PUT /users/settings`. Both fields are required: [callsign] must match the server's
 * callsign rule (see `validateCallsign`), [preferredLanguage] is `en` or `nl`.
 */
@Serializable
data class UpdateUserSettingsRequest(
    val callsign: String,
    val preferredLanguage: String,
)

/**
 * The 400 body of the Users API (ASP.NET Core validation problem). [errors] maps a field name
 * (`callsign`, `preferredLanguage`) to its messages.
 */
@Serializable
data class ValidationProblem(
    val errors: Map<String, List<String>> = emptyMap(),
)
