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
