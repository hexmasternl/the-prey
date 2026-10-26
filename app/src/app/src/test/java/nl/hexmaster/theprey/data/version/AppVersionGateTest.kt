package nl.hexmaster.theprey.data.version

import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.launch
import kotlinx.coroutines.test.runCurrent
import kotlinx.coroutines.test.runTest
import nl.hexmaster.theprey.auth.SessionState
import nl.hexmaster.theprey.auth.aProfile
import nl.hexmaster.theprey.data.game.ActiveGameResult
import nl.hexmaster.theprey.data.game.UnusedGameRepository
import okhttp3.ResponseBody.Companion.toResponseBody
import org.junit.Assert.assertEquals
import org.junit.Test
import retrofit2.Response
import java.io.IOException

class AppVersionGateTest {

    private class FakeVersionApi(var answer: () -> Response<Unit> = { Response.success(204, Unit) }) : VersionApi {
        val requests = mutableListOf<VersionCheckRequest>()

        override suspend fun check(request: VersionCheckRequest): Response<Unit> {
            requests += request
            return answer()
        }
    }

    private class FakeGameRepository(var result: ActiveGameResult = ActiveGameResult.None) : UnusedGameRepository() {
        var calls = 0

        override suspend fun activeGame(): ActiveGameResult {
            calls++
            return result
        }
    }

    private class Setup(
        session: SessionState = SessionState.SignedIn(aProfile()),
        debug: Boolean = false,
    ) {
        val api = FakeVersionApi()
        val games = FakeGameRepository()
        val session = MutableStateFlow(session)
        val gate = AppVersionGate(api, VERSION, debug, this.session, games)
    }

    @Test
    fun check_ShouldOpen_WhenUpToDate() = runTest {
        val setup = Setup()

        setup.gate.check()

        assertEquals(GateState.Open, setup.gate.state.value)
        assertEquals(listOf(VersionCheckRequest(VERSION)), setup.api.requests)
        assertEquals(0, setup.games.calls)
    }

    @Test
    fun check_ShouldBlock_WhenUpdateRequiredAndSignedOut() = runTest {
        val setup = Setup(session = SessionState.SignedOut())
        setup.api.answer = { conflict() }

        setup.gate.check()

        assertEquals(GateState.Blocked, setup.gate.state.value)
        assertEquals(0, setup.games.calls)
    }

    @Test
    fun check_ShouldDefer_WhenUpdateRequiredDuringAGame() = runTest {
        val setup = Setup()
        setup.api.answer = { conflict() }
        setup.games.result = ActiveGameResult.Active("game-1")

        setup.gate.check()

        assertEquals(GateState.Deferred, setup.gate.state.value)
    }

    @Test
    fun check_ShouldBlock_WhenUpdateRequiredWithoutAGame() = runTest {
        val setup = Setup()
        setup.api.answer = { conflict() }
        setup.games.result = ActiveGameResult.None

        setup.gate.check()

        assertEquals(GateState.Blocked, setup.gate.state.value)
    }

    @Test
    fun check_ShouldDefer_WhenUpdateRequiredAndTheGameIsUnknown() = runTest {
        val setup = Setup()
        setup.api.answer = { conflict() }
        setup.games.result = ActiveGameResult.Failed

        setup.gate.check()

        assertEquals(GateState.Deferred, setup.gate.state.value)
    }

    @Test
    fun check_ShouldStayOpen_WhenTheAnswerIsNotAConflict() = runTest {
        val answers: List<() -> Response<Unit>> = listOf(
            { error(400) },
            { error(404) },
            { error(500) },
            { throw IOException("offline") },
        )
        for (answer in answers) {
            val setup = Setup()
            setup.api.answer = answer

            setup.gate.check()

            assertEquals(GateState.Open, setup.gate.state.value)
        }
    }

    @Test
    fun check_ShouldReopen_WhenAnUpToDateAnswerFollowsABlock() = runTest {
        val setup = Setup(session = SessionState.SignedOut())
        setup.api.answer = { conflict() }
        setup.gate.check()
        setup.api.answer = { Response.success(204, Unit) }

        setup.gate.check()

        assertEquals(GateState.Open, setup.gate.state.value)
    }

    @Test
    fun check_ShouldStayOpen_WhenDebugBuild() = runTest {
        val setup = Setup(session = SessionState.SignedOut(), debug = true)
        setup.api.answer = { conflict() }

        setup.gate.check()

        assertEquals(GateState.Open, setup.gate.state.value)
    }

    @Test
    fun check_ShouldWaitForTheRestoredSession_WhenItIsStillUnknown() = runTest {
        val setup = Setup(session = SessionState.Unknown)
        setup.api.answer = { conflict() }
        setup.games.result = ActiveGameResult.Active("game-1")

        val job = launch { setup.gate.check() }
        runCurrent()
        assertEquals(GateState.Open, setup.gate.state.value)
        setup.session.value = SessionState.SignedIn(aProfile())
        job.join()

        assertEquals(GateState.Deferred, setup.gate.state.value)
    }

    @Test
    fun decide_ShouldIgnoreTheGame_WhenSignedOut() {
        assertEquals(
            GateState.Blocked,
            decide(VersionCheck.UpdateRequired, signedIn = false, activeGame = ActiveGameResult.Active("game-1")),
        )
        assertEquals(GateState.Open, decide(VersionCheck.Unknown, signedIn = true, activeGame = null))
    }

    private companion object {
        const val VERSION = "1.4.2"

        fun conflict(): Response<Unit> = error(409)

        fun error(code: Int): Response<Unit> = Response.error(code, "".toResponseBody())
    }
}
