package nl.hexmaster.theprey.ui.games.lobby

import nl.hexmaster.theprey.deeplink.joinLink
import org.junit.Assert.assertTrue
import org.junit.Test
import java.io.File

/** The share text as built in `LobbyScreen` (link, then code), read from both translations. */
class LobbyShareMessageTest {

    private val gameId = "3f2b8c1e-0d4a-4c1b-9a7e-2b6f1c9d8e01"

    /** `lobby_share_message` from [values]; unit tests run in the module directory. */
    private fun template(values: String): String {
        val xml = File("src/main/res/$values/strings.xml").readText()
        val raw = Regex("""<string name="lobby_share_message">(.*?)</string>""").find(xml)!!.groupValues[1]
        return raw.replace("\\n", "\n").replace("\\'", "'")
    }

    @Test
    fun lobbyShareMessage_ShouldContainTheLinkAndEndWithTheCode_InEnglishAndDutch() {
        listOf("values", "values-nl").forEach { values ->
            val message = template(values).format(joinLink(gameId, "4200"), "4200")

            assertTrue(values, message.contains("https://theprey.nl/games/join/$gameId/4200"))
            assertTrue(values, message.endsWith("4200"))
        }
    }
}
