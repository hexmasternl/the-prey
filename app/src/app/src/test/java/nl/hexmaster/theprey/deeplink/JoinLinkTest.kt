package nl.hexmaster.theprey.deeplink

import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test

class JoinLinkTest {

    private val id = "3f2b8c1e-0d4a-4c1b-9a7e-2b6f1c9d8e01"

    @Test
    fun parseJoinLink_ShouldReturnTheInvite_WhenLinkIsValid() {
        assertEquals(JoinInvite(id, "4291"), parseJoinLink("https://theprey.nl/games/join/$id/4291"))
    }

    @Test
    fun parseJoinLink_ShouldAcceptATrailingSlashAndLowercaseTheId_WhenLinkIsValid() {
        assertEquals(JoinInvite(id, "0042"), parseJoinLink("https://THEPREY.nl/games/join/${id.uppercase()}/0042/"))
    }

    @Test
    fun joinLink_ShouldRoundTrip_WhenParsedAgain() {
        val link = joinLink(id.uppercase(), "4200")

        assertEquals("https://theprey.nl/games/join/$id/4200", link)
        assertEquals(JoinInvite(id, "4200"), parseJoinLink(link))
    }

    @Test
    fun parseJoinLink_ShouldReturnNull_WhenLinkIsNotAnInvitation() {
        listOf(
            "https://theprey.nl/games/join/$id",
            "https://theprey.nl/games/join/$id/",
            "https://theprey.nl/games/join/not-a-guid/4291",
            "https://theprey.nl/games/join/$id/429",
            "https://theprey.nl/games/join/$id/42910",
            "https://theprey.nl/games/join/$id/42a1",
            "https://example.com/games/join/$id/4291",
            "https://www.theprey.nl/games/join/$id/4291",
            "http://theprey.nl/games/join/$id/4291",
            "https://theprey.nl/games/join/$id/4291/extra",
            "https://theprey.nl/en/games/join/$id/4291",
            "theprey://join?gameId=$id",
            "not a uri at all",
            "",
        ).forEach { link -> assertNull(link, parseJoinLink(link)) }
    }
}
