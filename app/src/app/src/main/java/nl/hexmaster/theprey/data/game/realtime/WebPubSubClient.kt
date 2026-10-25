package nl.hexmaster.theprey.data.game.realtime

import kotlinx.coroutines.channels.awaitClose
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.callbackFlow
import kotlinx.coroutines.flow.flow
import kotlinx.coroutines.flow.emitAll
import nl.hexmaster.theprey.data.game.GameRepository
import nl.hexmaster.theprey.data.game.TokenResult
import okhttp3.Request
import okhttp3.Response
import okhttp3.WebSocket
import okhttp3.WebSocketListener

/** What one realtime connection attempt reports. The flow completes after [Closed]. */
sealed interface RealtimeEvent {
    /** The socket joined the game's group; messages follow. */
    data object Joined : RealtimeEvent

    data class Message(val envelope: RealtimeEnvelope) : RealtimeEvent

    /**
     * The connection ended. [forbidden] means the caller may no longer see the game (the token
     * request was refused): do not reconnect.
     */
    data class Closed(val forbidden: Boolean = false) : RealtimeEvent
}

/** One realtime connection to a game's group; collecting [connect] opens it, cancelling closes it. */
fun interface RealtimeConnection {
    fun connect(gameId: String): Flow<RealtimeEvent>
}

/**
 * The Web PubSub socket: fetches a fresh group-scoped access URL, opens a WebSocket with the
 * `json.webpubsub.azure.v1` subprotocol and joins the group named after the game. Server → client
 * only: `joinGroup` is the one frame it sends.
 */
class WebPubSubClient(
    private val games: GameRepository,
    /** Opens the socket; the app passes a dedicated OkHttp client's `newWebSocket`. */
    private val newWebSocket: (Request, WebSocketListener) -> WebSocket,
    private val frames: WebPubSubFrames = WebPubSubFrames(),
) : RealtimeConnection {

    override fun connect(gameId: String): Flow<RealtimeEvent> = flow {
        when (val token = games.notificationsUrl(gameId)) {
            TokenResult.Forbidden -> emit(RealtimeEvent.Closed(forbidden = true))
            TokenResult.Failed -> emit(RealtimeEvent.Closed())
            is TokenResult.Ok -> emitAll(socket(gameId, token.url))
        }
    }

    private fun socket(gameId: String, url: String): Flow<RealtimeEvent> = callbackFlow {
        val request = Request.Builder()
            .url(url)
            .header("Sec-WebSocket-Protocol", RealtimeProtocol.SUBPROTOCOL)
            .build()

        val listener = object : WebSocketListener() {
            override fun onOpen(webSocket: WebSocket, response: Response) {
                webSocket.send(frames.joinGroup(gameId, JOIN_ACK_ID))
            }

            override fun onMessage(webSocket: WebSocket, text: String) {
                when (val frame = frames.parse(text)) {
                    is WebPubSubFrame.Ack -> if (frame.ackId == JOIN_ACK_ID) {
                        if (WebPubSubFrames.isJoined(frame)) {
                            trySend(RealtimeEvent.Joined)
                        } else {
                            // Not allowed into the group with this token: drop it and let the caller retry.
                            webSocket.close(NORMAL_CLOSURE, null)
                        }
                    }
                    is WebPubSubFrame.Message -> trySend(RealtimeEvent.Message(frame.envelope))
                    WebPubSubFrame.Other -> Unit
                }
            }

            override fun onClosing(webSocket: WebSocket, code: Int, reason: String) {
                webSocket.close(NORMAL_CLOSURE, null)
            }

            override fun onClosed(webSocket: WebSocket, code: Int, reason: String) {
                trySend(RealtimeEvent.Closed())
                channel.close()
            }

            override fun onFailure(webSocket: WebSocket, t: Throwable, response: Response?) {
                trySend(RealtimeEvent.Closed())
                channel.close()
            }
        }

        val webSocket = newWebSocket(request, listener)
        awaitClose { webSocket.close(NORMAL_CLOSURE, null) }
    }

    private companion object {
        const val JOIN_ACK_ID = 1
        const val NORMAL_CLOSURE = 1000
    }
}
