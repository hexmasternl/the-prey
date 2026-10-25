package nl.hexmaster.theprey.data.game.realtime

import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.launch
import kotlinx.coroutines.test.UnconfinedTestDispatcher
import kotlinx.coroutines.test.runTest
import kotlinx.serialization.json.Json
import kotlinx.serialization.json.jsonObject
import kotlinx.serialization.json.jsonPrimitive
import nl.hexmaster.theprey.data.game.TokenResult
import nl.hexmaster.theprey.data.game.UnusedGameRepository
import okhttp3.Protocol
import okhttp3.Request
import okhttp3.Response
import okhttp3.WebSocket
import okhttp3.WebSocketListener
import okio.ByteString
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

@OptIn(ExperimentalCoroutinesApi::class)
class WebPubSubClientTest {

    private class FakeGames(var token: TokenResult) : UnusedGameRepository() {
        override suspend fun notificationsUrl(gameId: String): TokenResult = token
    }

    private class FakeWebSocket(private val request: Request) : WebSocket {
        val sent = mutableListOf<String>()
        var closed = false

        override fun request(): Request = request
        override fun queueSize(): Long = 0
        override fun send(text: String): Boolean = sent.add(text)
        override fun send(bytes: ByteString): Boolean = false
        override fun close(code: Int, reason: String?): Boolean {
            closed = true
            return true
        }
        override fun cancel() {
            closed = true
        }
    }

    private var request: Request? = null
    private var listener: WebSocketListener? = null
    private var socket: FakeWebSocket? = null

    private fun client(token: TokenResult) = WebPubSubClient(FakeGames(token), { req, l ->
        request = req
        listener = l
        FakeWebSocket(req).also { socket = it }
    })

    private fun open() {
        val req = request!!
        listener!!.onOpen(socket!!, Response.Builder().request(req).protocol(Protocol.HTTP_1_1).code(101).message("").build())
    }

    @Test
    fun connect_ShouldOpenSubprotocolSocketAndJoinGroup_WhenTokenIsGranted() = runTest {
        val events = mutableListOf<RealtimeEvent>()
        backgroundScope.launch(UnconfinedTestDispatcher(testScheduler)) {
            client(TokenResult.Ok("wss://example.webpubsub.azure.com/client/hubs/game?access_token=t")).connect("g1").collect { events += it }
        }

        open()
        assertEquals(RealtimeProtocol.SUBPROTOCOL, request!!.header("Sec-WebSocket-Protocol"))
        val join = Json.parseToJsonElement(socket!!.sent.single()).jsonObject
        assertEquals("joinGroup", join.getValue("type").jsonPrimitive.content)
        assertEquals("g1", join.getValue("group").jsonPrimitive.content)

        listener!!.onMessage(socket!!, """{"type":"ack","ackId":1,"success":true}""")
        listener!!.onMessage(
            socket!!,
            """{"type":"message","from":"group","group":"g1","dataType":"json",
               "data":{"v":1,"type":"participant-removed","gameId":"g1","seq":3,"data":{"userId":"u"}}}""",
        )

        assertEquals(RealtimeEvent.Joined, events[0])
        assertEquals("participant-removed", (events[1] as RealtimeEvent.Message).envelope.type)
    }

    @Test
    fun connect_ShouldEmitJoined_WhenJoinAckIsDuplicate() = runTest {
        val events = mutableListOf<RealtimeEvent>()
        backgroundScope.launch(UnconfinedTestDispatcher(testScheduler)) {
            client(TokenResult.Ok("wss://example/client")).connect("g1").collect { events += it }
        }
        open()

        listener!!.onMessage(socket!!, """{"type":"ack","ackId":1,"success":false,"error":{"name":"Duplicate"}}""")

        assertEquals(listOf<RealtimeEvent>(RealtimeEvent.Joined), events)
    }

    @Test
    fun connect_ShouldEmitClosedAndComplete_WhenSocketFails() = runTest {
        val events = mutableListOf<RealtimeEvent>()
        val job = backgroundScope.launch(UnconfinedTestDispatcher(testScheduler)) {
            client(TokenResult.Ok("wss://example/client")).connect("g1").collect { events += it }
        }
        open()

        listener!!.onFailure(socket!!, RuntimeException("dropped"), null)

        assertEquals(listOf<RealtimeEvent>(RealtimeEvent.Closed()), events)
        assertTrue(job.isCompleted)
    }

    @Test
    fun connect_ShouldReportForbiddenWithoutSocket_WhenTokenIsRefused() = runTest {
        val events = mutableListOf<RealtimeEvent>()
        backgroundScope.launch(UnconfinedTestDispatcher(testScheduler)) {
            client(TokenResult.Forbidden).connect("g1").collect { events += it }
        }

        assertEquals(listOf<RealtimeEvent>(RealtimeEvent.Closed(forbidden = true)), events)
        assertNull(socket)
    }

    @Test
    fun connect_ShouldCloseSocket_WhenCollectorIsCancelled() = runTest {
        val job = backgroundScope.launch(UnconfinedTestDispatcher(testScheduler)) {
            client(TokenResult.Ok("wss://example/client")).connect("g1").collect { }
        }
        open()

        job.cancel()

        assertTrue(socket!!.closed)
    }
}
