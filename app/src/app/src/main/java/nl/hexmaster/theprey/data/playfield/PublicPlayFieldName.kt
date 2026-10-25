package nl.hexmaster.theprey.data.playfield

/**
 * The public-name convention `CC, City, Fieldname` (e.g. `NL, Amsterdam, Vondelpark`): a 2–4 letter
 * upper-case country code, a city starting with an upper-case letter, and a field name starting with
 * a letter or digit, separated by ", ".
 *
 * Mirrors `PlayField.IsPublicEligibleName` in
 * `server/src/PlayFields/HexMaster.ThePrey.PlayFields/DomainModels/PlayField.cs`, which is the source
 * of truth: keep the pattern identical (the unit tests replay the server's cases). `’` is U+2019.
 */
private val PublicNameRegex =
    Regex("""^[A-Z]{2,4}, \p{Lu}[\p{L} '’.-]*, [\p{L}\p{N}][\p{L}\p{N} &'’.-]*$""")

/** Whether the trimmed [name] may be public; the server forces any other name private. */
fun isPublicEligibleName(name: String): Boolean = name.isNotBlank() && PublicNameRegex.matches(name.trim())
