package nl.hexmaster.theprey.ui.settings

/**
 * Client copy of the server's callsign rule: `User.CallsignPattern` in
 * server/src/Users/HexMaster.ThePrey.Users/DomainModels/User.cs. Keep the two in step; the
 * server still has the last word (a 400 is shown at the field).
 */
private val CallsignPattern = Regex("""^[a-zA-Z0-9 \-_&$#@]{3,30}$""")

/** The outcome of checking a callsign draft. */
sealed interface CallsignCheck {
    /** Can be saved; [callsign] is the trimmed value to send. */
    data class Valid(val callsign: String) : CallsignCheck

    /** Breaks the rule: 3 to 30 letters, digits, spaces or `- _ & $ # @`. */
    data object Invalid : CallsignCheck

    /** Same as the stored callsign: nothing to save. */
    data object Unchanged : CallsignCheck
}

/** Checks [input] against the server's rule after trimming, compared to the [current] callsign. */
fun validateCallsign(input: String, current: String): CallsignCheck {
    val trimmed = input.trim()
    return when {
        trimmed == current.trim() -> CallsignCheck.Unchanged
        CallsignPattern.matches(trimmed) -> CallsignCheck.Valid(trimmed)
        else -> CallsignCheck.Invalid
    }
}

/** Whether [callsign] (as stored) would be accepted by the server, e.g. to send along with a language change. */
fun isValidCallsign(callsign: String): Boolean = CallsignPattern.matches(callsign)
