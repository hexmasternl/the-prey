package nl.hexmaster.theprey.data.game

import kotlinx.serialization.Serializable

/** `GET /games/{id}/tag-candidates`: the preys the hunter may tag right now, judged by the server. */
@Serializable
data class TagCandidatesDto(
    val rangeMeters: Double,
    val candidates: List<TagCandidateDto> = emptyList(),
)

/** One prey within tagging range. */
@Serializable
data class TagCandidateDto(
    val userId: String,
    val callsign: String,
    val state: String,
    val distanceMeters: Double,
)

/** Outcome of loading the tag candidates. */
sealed interface TagCandidatesResult {
    data class Ok(val candidates: TagCandidatesDto) : TagCandidatesResult

    /** No answer, a 403/404 or a server error: the sheet offers a retry. */
    data object Failed : TagCandidatesResult
}

/** Outcome of `POST /games/{id}/participants/{participantId}/tag`. */
sealed interface TagResult {
    /** 204: the prey is tagged. */
    data object Tagged : TagResult

    /** 409: out of range by now, already tagged, or the game moved on. */
    data object NoLongerTaggable : TagResult

    /** 403/404, another server answer, or no answer: nothing changed. */
    data object Failed : TagResult
}
