package nl.hexmaster.theprey.data.playfield

import kotlinx.serialization.builtins.ListSerializer
import kotlinx.serialization.json.Json
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test

class PlayFieldSummaryDtoTest {

    // Same configuration as the container's Json.
    private val json = Json { ignoreUnknownKeys = true }

    @Test
    fun decode_ShouldReadEveryField_WhenServerSendsAListWithAndWithoutCenter() {
        val payload = """
            [
              {"id":"6a1f0c2e-0000-4000-8000-000000000001","name":"Vondelpark",
               "ownerId":"9b2e1d3f-0000-4000-8000-000000000002","isPublic":true,
               "lastUpdatedOn":"2026-10-20T14:03:12.5+02:00",
               "centerCoordinates":{"latitude":52.358,"longitude":4.8686}},
              {"id":"6a1f0c2e-0000-4000-8000-000000000003","name":"Backyard",
               "ownerId":"9b2e1d3f-0000-4000-8000-000000000002","isPublic":false,
               "lastUpdatedOn":"2026-10-21T08:00:00+00:00","centerCoordinates":null,
               "pointCount":4}
            ]
        """.trimIndent()

        val decoded = json.decodeFromString(ListSerializer(PlayFieldSummaryDto.serializer()), payload)

        assertEquals(
            PlayFieldSummaryDto(
                id = "6a1f0c2e-0000-4000-8000-000000000001",
                name = "Vondelpark",
                ownerId = "9b2e1d3f-0000-4000-8000-000000000002",
                isPublic = true,
                lastUpdatedOn = "2026-10-20T14:03:12.5+02:00",
                centerCoordinates = GpsCoordinateDto(52.358, 4.8686),
            ),
            decoded[0],
        )
        assertEquals("Backyard", decoded[1].name)
        assertEquals(false, decoded[1].isPublic)
        assertNull(decoded[1].centerCoordinates)
    }

    @Test
    fun decode_ShouldDefaultCenterToNull_WhenFieldIsAbsent() {
        val payload = """
            {"id":"a","name":"Park","ownerId":"o","isPublic":false,"lastUpdatedOn":"2026-10-21T08:00:00Z"}
        """.trimIndent()

        assertNull(json.decodeFromString(PlayFieldSummaryDto.serializer(), payload).centerCoordinates)
    }
}
