package nl.hexmaster.theprey.data.playfield

import kotlinx.serialization.json.Json
import nl.hexmaster.theprey.data.location.GeoPosition
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

class PlayFieldDtoTest {

    // Same configuration as the container's Json.
    private val json = Json { ignoreUnknownKeys = true }

    @Test
    fun decode_ShouldReadEveryField_WhenServerSendsCenterCoordinates() {
        val payload = """
            {"id":"6a1f0c2e-0000-4000-8000-000000000001","name":"NL, Amsterdam, Vondelpark",
             "ownerId":"9b2e1d3f-0000-4000-8000-000000000002","isPublic":true,
             "points":[{"latitude":52.36,"longitude":4.86},{"latitude":52.36,"longitude":4.88},
                       {"latitude":52.35,"longitude":4.87}],
             "lastUpdatedOn":"2026-10-20T14:03:12.1234567+02:00",
             "centerCoordinates":{"latitude":52.3566,"longitude":4.87}}
        """.trimIndent()

        val decoded = json.decodeFromString<PlayFieldDto>(payload)

        assertEquals(
            PlayFieldDto(
                id = "6a1f0c2e-0000-4000-8000-000000000001",
                name = "NL, Amsterdam, Vondelpark",
                ownerId = "9b2e1d3f-0000-4000-8000-000000000002",
                isPublic = true,
                points = listOf(
                    GpsCoordinateDto(52.36, 4.86),
                    GpsCoordinateDto(52.36, 4.88),
                    GpsCoordinateDto(52.35, 4.87),
                ),
                lastUpdatedOn = "2026-10-20T14:03:12.1234567+02:00",
                centerCoordinates = GpsCoordinateDto(52.3566, 4.87),
            ),
            decoded,
        )
    }

    @Test
    fun decode_ShouldIgnoreUnknownFields_WhenCenterIsNullOrMissing() {
        val withNull = """
            {"id":"p1","name":"Backyard","ownerId":"o1","isPublic":false,"points":[],
             "lastUpdatedOn":"2026-10-21T08:00:00+00:00","centerCoordinates":null,"pointCount":0}
        """.trimIndent()
        val withoutCenter = """
            {"id":"p1","name":"Backyard","ownerId":"o1","isPublic":false,"points":[],
             "lastUpdatedOn":"2026-10-21T08:00:00+00:00","somethingNew":{"a":1}}
        """.trimIndent()

        listOf(withNull, withoutCenter).forEach { payload ->
            val decoded = json.decodeFromString<PlayFieldDto>(payload)
            assertNull(decoded.centerCoordinates)
            assertTrue(decoded.points.isEmpty())
            assertEquals("2026-10-21T08:00:00+00:00", decoded.lastUpdatedOn)
        }
    }

    @Test
    fun encode_ShouldEchoLastUpdatedOnByteForByte_WhenRequestIsSerialized() {
        val lastUpdatedOn = "2026-10-20T14:03:12.1234567+02:00"
        val request = UpsertPlayFieldRequest(
            name = "NL, Amsterdam, Vondelpark",
            isPublic = true,
            points = listOf(GpsCoordinateDto(52.36, 4.86)),
            lastUpdatedOn = lastUpdatedOn,
        )

        val encoded = json.encodeToString(UpsertPlayFieldRequest.serializer(), request)

        assertEquals(
            """{"name":"NL, Amsterdam, Vondelpark","isPublic":true,""" +
                """"points":[{"latitude":52.36,"longitude":4.86}],"lastUpdatedOn":"$lastUpdatedOn"}""",
            encoded,
        )
    }

    @Test
    fun positions_ShouldMapPointsInOrder_WhenConvertingBothWays() {
        val dto = PlayFieldDto(
            id = "p1",
            name = "Field",
            ownerId = "o1",
            isPublic = false,
            points = listOf(GpsCoordinateDto(1.0, 2.0), GpsCoordinateDto(3.0, 4.0)),
            lastUpdatedOn = "2026-10-21T08:00:00+00:00",
        )

        assertEquals(listOf(GeoPosition(1.0, 2.0), GeoPosition(3.0, 4.0)), dto.positions())
        assertEquals(dto.points, dto.positions().map { it.toGpsCoordinateDto() })
    }
}
