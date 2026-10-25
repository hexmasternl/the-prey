package nl.hexmaster.theprey.data.game

import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.Job
import kotlinx.coroutines.channels.Channel
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.consumeAsFlow
import kotlinx.coroutines.launch
import kotlinx.coroutines.test.TestScope
import kotlinx.coroutines.test.UnconfinedTestDispatcher
import kotlinx.coroutines.test.advanceTimeBy
import kotlinx.coroutines.test.runCurrent
import kotlinx.coroutines.test.runTest
import kotlinx.serialization.json.Json
import nl.hexmaster.theprey.data.game.realtime.RealtimeConnection
import nl.hexmaster.theprey.data.game.realtime.RealtimeEnvelope
import nl.hexmaster.theprey.data.game.realtime.RealtimeEvent
import nl.hexmaster.theprey.data.game.realtime.RealtimeProtocol
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

@OptIn(ExperimentalCoroutinesApi::class)
class GameStateServiceTest {

    private val game = GameDto(
        id = "g1",
        status = GameStatuses.LOBBY,
        configuration = GameConfigurationDto(30),
        participants = listOf(ParticipantDto("me", PlayerStates.ACTIVE)),
        ownerUserId = "owner",
    )

    private class FakeGames(var snapshot: () -> SnapshotResult) : UnusedGameRepository() {
        var snapshots = 0

        override suspend fun gameState(gameId: String): SnapshotResult {
            snapshots++
            return snapshot()
        }
    }

    /** Each connect opens a channel the test drives; [connects] counts attempts. */
    private class FakeConnection : RealtimeConnection {
        val sockets = mutableListOf<Channel<RealtimeEvent>>()
        val connects get() = sockets.size

        override fun connect(gameId: String): Flow<RealtimeEvent> =
            Channel<RealtimeEvent>(Channel.UNLIMITED).also { sockets += it }.consumeAsFlow()

        /** The latest socket reports [event]. */
        fun emit(event: RealtimeEvent) {
            sockets.last().trySend(event)
        }

        /** The latest socket drops. */
        fun drop() {
            sockets.last().trySend(RealtimeEvent.Closed())
            sockets.last().close()
        }
    }

    private val games = FakeGames { SnapshotResult.Ok(FullGameStateDto(game, "2026-08-31T11:40:00+00:00")) }
    private val connection = FakeConnection()
    private val ended = mutableListOf<String>()

    private fun TestScope.service() = GameStateService(
        games = games,
        connection = connection,
        myUserId = { "me" },
        onGameEnded = { ended += it },
        scope = CoroutineScope(backgroundScope.coroutineContext + UnconfinedTestDispatcher(testScheduler)),
    )

    private fun TestScope.observe(service: GameStateService, gameId: String = "g1"): Pair<MutableList<GameSession>, Job> {
        val seen = mutableListOf<GameSession>()
        val job = backgroundScope.launch(UnconfinedTestDispatcher(testScheduler)) { service.session(gameId).collect { seen += it } }
        return seen to job
    }

    private fun envelope(type: String, seq: Long, data: String) =
        RealtimeEnvelope(1, type, "g1", seq, Json.parseToJsonElement(data))

    private fun participant(id: String) =
        """{"userId":"$id","displayName":"$id","isReady":false,"state":"Active","lastKnownLocation":null,"hasActivePenalty":false}"""

    @Test
    fun session_ShouldLoadSnapshotThenConnect_WhenObserved() = runTest {
        val sut = service()
        val (seen, _) = observe(sut)

        val live = seen.last() as GameSession.Live
        assertEquals("g1", live.snapshot.game.id)
        assertEquals(ConnectionState.Reconnecting, live.connection)
        assertEquals(1, games.snapshots)
        assertEquals(1, connection.connects)
    }

    @Test
    fun session_ShouldResyncAndMarkConnected_WhenSocketJoins() = runTest {
        val sut = service()
        val (seen, _) = observe(sut)

        connection.emit(RealtimeEvent.Joined)

        assertEquals(2, games.snapshots)
        assertEquals(ConnectionState.Connected, (seen.last() as GameSession.Live).connection)
    }

    @Test
    fun session_ShouldApplyDelta_WhenMessageArrives() = runTest {
        val sut = service()
        val (seen, _) = observe(sut)
        connection.emit(RealtimeEvent.Joined)

        connection.emit(RealtimeEvent.Message(envelope(RealtimeProtocol.MessageTypes.PARTICIPANT_JOINED, 10, participant("new"))))

        val live = seen.last() as GameSession.Live
        assertEquals(listOf("me", "new"), live.snapshot.game.participants.map { it.userId })
        assertEquals(10L, live.lastSeq)
    }

    @Test
    fun session_ShouldPullSnapshot_WhenSeqHasAGap() = runTest {
        val sut = service()
        observe(sut)
        connection.emit(RealtimeEvent.Joined)
        connection.emit(RealtimeEvent.Message(envelope(RealtimeProtocol.MessageTypes.PARTICIPANT_JOINED, 10, participant("a"))))
        val before = games.snapshots

        connection.emit(RealtimeEvent.Message(envelope(RealtimeProtocol.MessageTypes.PARTICIPANT_JOINED, 12, participant("b"))))

        assertEquals(before + 1, games.snapshots)
    }

    @Test
    fun session_ShouldResyncEveryThreeMinutes_WhenNothingIsMissed() = runTest {
        val sut = service()
        observe(sut)
        connection.emit(RealtimeEvent.Joined)
        val before = games.snapshots

        advanceTimeBy(GameStateService.RESYNC_INTERVAL_MS - 1)
        runCurrent()
        assertEquals(before, games.snapshots)

        advanceTimeBy(2)
        runCurrent()
        assertEquals(before + 1, games.snapshots)

        advanceTimeBy(GameStateService.RESYNC_INTERVAL_MS)
        runCurrent()
        assertEquals(before + 2, games.snapshots)
    }

    @Test
    fun session_ShouldReconnectWithBackoffAndResync_WhenSocketDrops() = runTest {
        val sut = service()
        val (seen, _) = observe(sut)
        connection.emit(RealtimeEvent.Joined)

        connection.drop()
        assertEquals(ConnectionState.Reconnecting, (seen.last() as GameSession.Live).connection)
        advanceTimeBy(999)
        runCurrent()
        assertEquals(1, connection.connects)
        advanceTimeBy(1)
        runCurrent()
        assertEquals(2, connection.connects)

        // Second drop without a join in between: the wait doubles.
        connection.drop()
        advanceTimeBy(1_999)
        runCurrent()
        assertEquals(2, connection.connects)
        advanceTimeBy(1)
        runCurrent()
        assertEquals(3, connection.connects)

        val before = games.snapshots
        connection.emit(RealtimeEvent.Joined)
        assertEquals(before + 1, games.snapshots)
        assertEquals(ConnectionState.Connected, (seen.last() as GameSession.Live).connection)
    }

    @Test
    fun backoffMillis_ShouldDoubleUpTo30Seconds_WhenAttemptsGrow() {
        assertEquals(
            listOf(1_000L, 2_000L, 4_000L, 8_000L, 16_000L, 30_000L, 30_000L),
            (0..6).map { GameStateService.backoffMillis(it) },
        )
    }

    @Test
    fun session_ShouldBeGoneAndStopReconnecting_WhenTokenIsForbidden() = runTest {
        val sut = service()
        val (seen, _) = observe(sut)

        connection.emit(RealtimeEvent.Closed(forbidden = true))
        connection.sockets.last().close()
        advanceTimeBy(60_000)
        runCurrent()

        assertEquals(GameSession.Gone(GoneReason.Removed), seen.last())
        assertEquals(1, connection.connects)
    }

    @Test
    fun session_ShouldBeGoneRemoved_WhenSnapshotIsForbidden() = runTest {
        games.snapshot = { SnapshotResult.Forbidden }
        val sut = service()
        val (seen, _) = observe(sut)

        assertEquals(GameSession.Gone(GoneReason.Removed), seen.last())
        assertEquals(0, connection.connects)
    }

    @Test
    fun session_ShouldFailAndRetryOnResync_WhenFirstSnapshotFails() = runTest {
        games.snapshot = { SnapshotResult.Failed }
        val sut = service()
        val (seen, _) = observe(sut)
        assertEquals(GameSession.Failed(retryable = true), seen.last())

        games.snapshot = { SnapshotResult.Ok(FullGameStateDto(game, "t")) }
        sut.resync("g1")

        assertTrue(seen.last() is GameSession.Live)
    }

    @Test
    fun replaceGame_ShouldSwapGameAndKeepSeq_WhenCommandSucceeds() = runTest {
        val sut = service()
        val (seen, _) = observe(sut)
        connection.emit(RealtimeEvent.Joined)
        connection.emit(RealtimeEvent.Message(envelope(RealtimeProtocol.MessageTypes.PARTICIPANT_JOINED, 4, participant("x"))))

        sut.replaceGame("g1", game.copy(hunterUserId = "me"))

        val live = seen.last() as GameSession.Live
        assertEquals("me", live.snapshot.game.hunterUserId)
        assertEquals(4L, live.lastSeq)
    }

    @Test
    fun session_ShouldCallOnGameEndedOnce_WhenGameEnds() = runTest {
        val sut = service()
        val (seen, _) = observe(sut)
        connection.emit(RealtimeEvent.Joined)

        connection.emit(RealtimeEvent.Message(envelope(RealtimeProtocol.MessageTypes.GAME_ENDED, 1, """{"outcome":"Cancelled"}""")))
        connection.emit(RealtimeEvent.Message(envelope(RealtimeProtocol.MessageTypes.GAME_ENDED, 2, """{"outcome":"Cancelled"}""")))

        assertEquals(GameSession.Gone(GoneReason.Cancelled), seen.last())
        assertEquals(listOf("g1"), ended)
    }

    @Test
    fun session_ShouldBeGoneCompleted_WhenSnapshotShowsCompletedGame() = runTest {
        games.snapshot = { SnapshotResult.Ok(FullGameStateDto(game.copy(status = GameStatuses.COMPLETED, outcome = "HuntersWin"), "t")) }
        val sut = service()
        val (seen, _) = observe(sut)

        assertEquals(GameSession.Gone(GoneReason.Completed), seen.last())
        assertEquals(listOf("g1"), ended)
    }

    @Test
    fun session_ShouldCloseSocketFiveSecondsAfterLastSubscriber_WhenScreenLeaves() = runTest {
        val sut = service()
        val (_, job) = observe(sut)
        val socket = connection.sockets.single()

        job.cancel()
        advanceTimeBy(GameStateService.STOP_TIMEOUT_MS - 1)
        runCurrent()
        assertTrue(!socket.isClosedForSend)

        advanceTimeBy(2)
        runCurrent()
        assertTrue(socket.isClosedForSend)
    }

    @Test
    fun session_ShouldKeepSocket_WhenNewSubscriberArrivesWithinGrace() = runTest {
        val sut = service()
        val (_, job) = observe(sut)
        job.cancel()
        advanceTimeBy(2_000)

        observe(sut)
        advanceTimeBy(10_000)
        runCurrent()

        assertEquals(1, connection.connects)
        assertTrue(!connection.sockets.single().isClosedForSend)
    }

    @Test
    fun close_ShouldEndSessionAndStartFreshNextTime_WhenPlayerLeft() = runTest {
        val sut = service()
        observe(sut)

        sut.close("g1")
        assertTrue(connection.sockets.single().isClosedForSend)

        observe(sut)
        assertEquals(2, connection.connects)
    }
}
