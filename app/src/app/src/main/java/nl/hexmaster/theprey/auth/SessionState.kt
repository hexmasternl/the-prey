package nl.hexmaster.theprey.auth

import android.content.Context
import kotlinx.coroutines.flow.StateFlow
import nl.hexmaster.theprey.data.user.UserDto

/** Who, if anyone, is signed in. */
sealed interface SessionState {
    /** The stored session has not been checked yet (app start). */
    data object Unknown : SessionState

    data class SignedOut(val reason: SignOutReason = SignOutReason.None) : SessionState

    data class SignedIn(val profile: UserDto) : SessionState
}

enum class SignOutReason {
    /** Never signed in, or the player signed out. */
    None,

    /** The session could not be renewed and was removed. */
    SessionExpired,
}

sealed interface SignInResult {
    data object Success : SignInResult

    /** The player closed the login page; not an error. */
    data object Cancelled : SignInResult

    data class Failed(val error: SignInError) : SignInResult
}

enum class SignInError {
    /** Login or identification could not reach the network. */
    Network,

    /** The account has no email address, which the Users API requires. */
    NoEmail,

    /** Anything else; details are logged, never shown. */
    Generic,
}

/** What screens need from the session: its state, and sign-in / sign-out. */
interface SessionController {
    val state: StateFlow<SessionState>

    /** [context] must be an Activity: the hosted login page is launched from it. */
    suspend fun signIn(context: Context): SignInResult

    suspend fun signOut(context: Context)
}
