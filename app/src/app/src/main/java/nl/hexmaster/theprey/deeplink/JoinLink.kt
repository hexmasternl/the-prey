package nl.hexmaster.theprey.deeplink

import java.net.URI
import java.net.URISyntaxException

/** An invitation to a game: its id (lower case) and its 4-digit code. */
data class JoinInvite(val gameId: String, val code: String)

private const val HOST = "theprey.nl"

private val JoinPath = Regex(
    """^/games/join/([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})/(\d{4})/?$""",
)

/**
 * The invitation in [uri], or null when it is not one: `https://theprey.nl/games/join/{guid}/{4 digits}`,
 * with an optional trailing slash. Keep the rules in step with `website/src/static/games/join/index.html`.
 */
fun parseJoinLink(uri: String): JoinInvite? {
    val parsed = try {
        URI(uri.trim())
    } catch (_: URISyntaxException) {
        return null
    }
    if (!parsed.scheme.equals("https", ignoreCase = true)) return null
    if (!parsed.host.equals(HOST, ignoreCase = true)) return null
    if (parsed.port != -1 && parsed.port != 443) return null
    val match = JoinPath.matchEntire(parsed.rawPath ?: return null) ?: return null
    return JoinInvite(gameId = match.groupValues[1].lowercase(), code = match.groupValues[2])
}

/** The public invitation link of a game; [parseJoinLink] reads it back. */
fun joinLink(gameId: String, code: String): String = "https://$HOST/games/join/${gameId.lowercase()}/$code"
