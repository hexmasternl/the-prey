package nl.hexmaster.theprey.ui

import android.content.Context
import kotlinx.coroutines.flow.MutableStateFlow
import nl.hexmaster.theprey.auth.SessionController
import nl.hexmaster.theprey.auth.SessionState
import nl.hexmaster.theprey.auth.SignInResult

class FakeSession(initial: SessionState) : SessionController {
    override val state = MutableStateFlow(initial)
    var signInResult: SignInResult = SignInResult.Success
    var signIns = 0

    override suspend fun signIn(context: Context): SignInResult {
        signIns++
        return signInResult
    }

    override suspend fun signOut(context: Context) {
        state.value = SessionState.SignedOut()
    }
}
