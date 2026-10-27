package nl.hexmaster.theprey.ui.playfields

import nl.hexmaster.theprey.data.location.GeoPosition
import nl.hexmaster.theprey.data.playfield.GpsCoordinateDto
import nl.hexmaster.theprey.data.playfield.PlayFieldSummaryDto
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test
import java.time.LocalDate

class PlayFieldItemTest {

    private fun dto(lastUpdatedOn: String = "2026-10-20T14:03:12.5+02:00", center: GpsCoordinateDto? = null) =
        PlayFieldSummaryDto(
            id = "p1",
            name = "Vondelpark",
            ownerId = "o1",
            isPublic = true,
            lastUpdatedOn = lastUpdatedOn,
            centerCoordinates = center,
        )

    @Test
    fun toItem_ShouldMapEveryField_WhenDtoIsComplete() {
        val item = dto(center = GpsCoordinateDto(52.358, 4.8686)).toItem()

        assertEquals(
            PlayFieldItem(
                id = "p1",
                name = "Vondelpark",
                isPublic = true,
                lastUpdated = LocalDate.of(2026, 10, 20),
                center = GeoPosition(52.358, 4.8686),
            ),
            item,
        )
    }

    @Test
    fun toItem_ShouldKeepServerDay_WhenOffsetIsNotUtc() {
        // 00:30 at +02:00 is still the 21st on the server's clock.
        assertEquals(LocalDate.of(2026, 10, 21), dto(lastUpdatedOn = "2026-10-21T00:30:00+02:00").toItem().lastUpdated)
    }

    @Test
    fun toItem_ShouldParseUtcDesignator_WhenDateEndsWithZ() {
        assertEquals(LocalDate.of(2026, 10, 21), dto(lastUpdatedOn = "2026-10-21T08:00:00Z").toItem().lastUpdated)
    }

    @Test
    fun toItem_ShouldHaveNoCenter_WhenDtoHasNoCoordinates() {
        assertNull(dto(center = null).toItem().center)
    }

    @Test
    fun toItem_ShouldHaveNoDate_WhenDateDoesNotParse() {
        assertNull(dto(lastUpdatedOn = "yesterday").toItem().lastUpdated)
    }

    @Test
    fun toItem_ShouldComputeDistanceOnTheDevice_WhenGivenTheOwnFix() {
        // 0.01° north is about 1112 m.
        val item = dto(center = GpsCoordinateDto(52.01, 5.0)).copy(distanceMeters = 5_000).toItem(from = GeoPosition(52.0, 5.0))

        assertEquals(1_112, item.distanceMeters)
    }

    @Test
    fun toItem_ShouldUseServerDistance_WhenNoOwnFixIsGiven() {
        assertEquals(5_000, dto(center = GpsCoordinateDto(52.01, 5.0)).copy(distanceMeters = 5_000).toItem().distanceMeters)
        assertNull(dto(center = GpsCoordinateDto(52.01, 5.0)).toItem().distanceMeters)
    }
}
