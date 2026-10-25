package nl.hexmaster.theprey.ui.playfields

import kotlinx.coroutines.CompletableDeferred
import nl.hexmaster.theprey.data.playfield.DeleteOutcome
import nl.hexmaster.theprey.data.playfield.GpsCoordinateDto
import nl.hexmaster.theprey.data.playfield.LoadOutcome
import nl.hexmaster.theprey.data.playfield.PlayFieldRepository
import nl.hexmaster.theprey.data.playfield.PlayFieldSummaryDto
import nl.hexmaster.theprey.data.playfield.SaveOutcome
import nl.hexmaster.theprey.data.playfield.UpsertPlayFieldRequest

/** A [PlayFieldRepository] whose calls wait until the test answers them, unless an automatic answer is set. */
class ScriptedPlayFieldRepository : PlayFieldRepository {
    var mineAnswer: (() -> Result<List<PlayFieldSummaryDto>>)? = { Result.success(emptyList()) }
    val mineCalls = mutableListOf<CompletableDeferred<Result<List<PlayFieldSummaryDto>>>>()
    var mineCount = 0

    val searches = mutableListOf<String>()
    val searchCalls = mutableListOf<CompletableDeferred<Result<List<PlayFieldSummaryDto>>>>()

    override suspend fun listMine(): Result<List<PlayFieldSummaryDto>> {
        mineCount++
        mineAnswer?.let { return it() }
        return CompletableDeferred<Result<List<PlayFieldSummaryDto>>>().also { mineCalls += it }.await()
    }

    override suspend fun searchPublic(query: String): Result<List<PlayFieldSummaryDto>> {
        searches += query
        return CompletableDeferred<Result<List<PlayFieldSummaryDto>>>().also { searchCalls += it }.await()
    }

    override suspend fun get(id: String): LoadOutcome = error("Not used")

    override suspend fun save(id: String, request: UpsertPlayFieldRequest): SaveOutcome = error("Not used")

    override suspend fun delete(id: String): DeleteOutcome = error("Not used")
}

fun playFieldDto(id: String, name: String = "Field $id", isPublic: Boolean = false) = PlayFieldSummaryDto(
    id = id,
    name = name,
    ownerId = "owner",
    isPublic = isPublic,
    lastUpdatedOn = "2026-10-20T14:03:12+02:00",
    centerCoordinates = GpsCoordinateDto(52.358, 4.8686),
)
