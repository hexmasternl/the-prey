package nl.hexmaster.theprey.data.playfield

import org.junit.Assert.assertEquals
import org.junit.Test

/**
 * Replays `PlayFieldTests.IsPublicEligibleName_*` from
 * `server/src/PlayFields/HexMaster.ThePrey.PlayFields.Tests/DomainModels/PlayFieldTests.cs`, so a
 * drift between the client and server rule shows up here. Add new server cases to [serverCases].
 */
class PublicPlayFieldNameTest {

    private val serverCases = listOf(
        "NL, Amsterdam, Vondelpark Arena" to true,
        "USA, New York, Central Park & Field" to true,
        "NL, Den Haag, Zuiderpark-West" to true,
        "DE, München, Englischer Garten" to true,
        "FR, Saint-Étienne, Le Parc" to true,
        "D, Berlin, Tiergarten" to false, // country code too short
        "NLAND, Amsterdam, Park" to false, // country code too long
        "nl, Amsterdam, Park" to false, // lowercase country code
        "NL, amsterdam, Park" to false, // lowercase city
        "NL, Amsterdam" to false, // no field name
        "City Park Arena" to false, // no convention at all
        "NL, Amsterdam, Park@Home" to false, // illegal char in field name
        "" to false,
        "   " to false,
    )

    private val specCases = listOf(
        "NL, Amsterdam, Vondelpark" to true,
        "BE, Liège, Parc de la Boverie 2" to true,
        "  NL, Amsterdam, Vondelpark  " to true, // surrounding spaces are ignored
        "nl, amsterdam, vondelpark" to false, // lower-case country code
        "Vondelpark" to false,
        "NL, Utrecht, Wilhelminapark" to true,
        "NL, ’s-Hertogenbosch, Zuiderpark" to false, // city must start with an upper-case letter
        "NL, Den Bosch, Prins Hendrik’s Park" to true, // typographic apostrophe (U+2019)
        "NL,Amsterdam,Vondelpark" to false, // separators are ", "
    )

    @Test
    fun isPublicEligibleName_ShouldMatchServer_ForEveryServerCase() {
        serverCases.forEach { (name, expected) ->
            assertEquals("\"$name\"", expected, isPublicEligibleName(name))
        }
    }

    @Test
    fun isPublicEligibleName_ShouldReturnExpected_ForSpecExamples() {
        specCases.forEach { (name, expected) ->
            assertEquals("\"$name\"", expected, isPublicEligibleName(name))
        }
    }
}
