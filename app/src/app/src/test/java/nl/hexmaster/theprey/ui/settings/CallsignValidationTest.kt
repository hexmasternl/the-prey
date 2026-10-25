package nl.hexmaster.theprey.ui.settings

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class CallsignValidationTest {

    @Test
    fun validateCallsign_ShouldBeInvalid_WhenTooShort() {
        assertEquals(CallsignCheck.Invalid, validateCallsign("ab", current = "NightOwl"))
    }

    @Test
    fun validateCallsign_ShouldBeInvalid_WhenTooShortAfterTrimming() {
        assertEquals(CallsignCheck.Invalid, validateCallsign("  ab  ", current = "NightOwl"))
    }

    @Test
    fun validateCallsign_ShouldBeInvalid_WhenEmpty() {
        assertEquals(CallsignCheck.Invalid, validateCallsign("", current = "NightOwl"))
    }

    @Test
    fun validateCallsign_ShouldBeInvalid_WhenTooLong() {
        assertEquals(CallsignCheck.Invalid, validateCallsign("a".repeat(31), current = "NightOwl"))
    }

    @Test
    fun validateCallsign_ShouldBeValid_WhenExactlyAtTheLimits() {
        assertEquals(CallsignCheck.Valid("abc"), validateCallsign("abc", current = "NightOwl"))
        assertEquals(CallsignCheck.Valid("a".repeat(30)), validateCallsign("a".repeat(30), current = "NightOwl"))
    }

    @Test
    fun validateCallsign_ShouldBeInvalid_WhenItHasANotAllowedCharacter() {
        assertEquals(CallsignCheck.Invalid, validateCallsign("Hunter!", current = "NightOwl"))
        assertEquals(CallsignCheck.Invalid, validateCallsign("Jäger", current = "NightOwl"))
        assertEquals(CallsignCheck.Invalid, validateCallsign("Owl.7", current = "NightOwl"))
    }

    @Test
    fun validateCallsign_ShouldTrim_WhenSurroundedBySpaces() {
        assertEquals(CallsignCheck.Valid("Owl 7"), validateCallsign("  Owl 7  ", current = "NightOwl"))
    }

    @Test
    fun validateCallsign_ShouldBeUnchanged_WhenEqualToCurrent() {
        assertEquals(CallsignCheck.Unchanged, validateCallsign("NightOwl", current = "NightOwl"))
        assertEquals(CallsignCheck.Unchanged, validateCallsign(" NightOwl ", current = "NightOwl"))
    }

    @Test
    fun validateCallsign_ShouldBeUnchanged_WhenEmptyWithoutCurrentCallsign() {
        assertEquals(CallsignCheck.Unchanged, validateCallsign("", current = ""))
    }

    @Test
    fun validateCallsign_ShouldBeValid_WhenUsingEveryAllowedSymbol() {
        assertEquals(CallsignCheck.Valid("Night Owl #7"), validateCallsign("Night Owl #7", current = ""))
        assertEquals(CallsignCheck.Valid("a-b_c&d\$e#f@g 0"), validateCallsign("a-b_c&d\$e#f@g 0", current = ""))
    }

    @Test
    fun isValidCallsign_ShouldMatchTheServerRule() {
        assertTrue(isValidCallsign("Night-Hawk_7"))
        assertFalse(isValidCallsign(""))
        assertFalse(isValidCallsign("Hunter!"))
    }
}
