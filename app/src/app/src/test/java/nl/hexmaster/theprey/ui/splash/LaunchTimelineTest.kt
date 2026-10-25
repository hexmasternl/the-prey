package nl.hexmaster.theprey.ui.splash

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class LaunchTimelineTest {

    @Test
    fun frameAt_ShouldBeHandOffPose_WhenSequenceStarts() {
        val frame = LaunchTimeline.frameAt(0)

        assertEquals(0f, frame.markLift, 0.0001f)
        assertEquals(1f, frame.markScale, 0.0001f)
        assertEquals(0f, frame.headFlash, 0.0001f)
        assertEquals(0f, frame.sweepAlpha, 0.0001f)
        assertTrue(frame.pings.isEmpty())
        assertEquals(0, frame.titleChars)
        assertEquals(0f, frame.taglineAlpha, 0.0001f)
        assertEquals(-1, frame.statusIndex)
        assertFalse(frame.isFinished)
    }

    @Test
    fun frameAt_ShouldHoldMarkStill_WhenWithinHandOffHold() {
        for (ms in 0L..300L step 50) {
            val frame = LaunchTimeline.frameAt(ms)
            assertEquals("lift at $ms ms", 0f, frame.markLift, 0.0001f)
            assertEquals("scale at $ms ms", 1f, frame.markScale, 0.0001f)
        }
    }

    @Test
    fun frameAt_ShouldRevealTitleMonotonically_WhenTimePasses() {
        var previous = 0
        for (ms in 0L until LaunchTimeline.TOTAL_MS step 16) {
            val chars = LaunchTimeline.frameAt(ms).titleChars
            assertTrue("title shrank at $ms ms", chars >= previous)
            previous = chars
        }
        assertEquals(LaunchTimeline.DEFAULT_TITLE_LENGTH, previous)
    }

    @Test
    fun frameAt_ShouldRevealWholeTitle_WhenTitleLengthDiffers() {
        assertEquals(12, LaunchTimeline.frameAt(LaunchTimeline.TOTAL_MS - 1, titleLength = 12).titleChars)
    }

    @Test
    fun frameAt_ShouldFinish_WhenTotalDurationElapsed() {
        assertTrue(LaunchTimeline.frameAt(LaunchTimeline.TOTAL_MS).isFinished)
        assertFalse(LaunchTimeline.frameAt(LaunchTimeline.TOTAL_MS - 1).isFinished)
    }

    @Test
    fun timeline_ShouldStayWithinSpecLimits_WhenConfigured() {
        assertTrue(LaunchTimeline.TOTAL_MS <= 3000L)
        assertTrue(LaunchTimeline.CROSSFADE_MS <= 400)
    }

    @Test
    fun frameAt_ShouldShowEveryElement_WhenSequenceIsUnderway() {
        val frames = (0L until LaunchTimeline.TOTAL_MS step 16).map { LaunchTimeline.frameAt(it) }

        assertTrue(frames.any { it.sweepAlpha > 0f })
        assertTrue(frames.any { it.pings.isNotEmpty() })
        assertTrue(frames.any { it.headFlash > 0.9f })
        assertTrue(frames.any { it.cursorVisible })
        assertEquals((0 until LaunchTimeline.STATUS_COUNT).toSet(), frames.map { it.statusIndex }.filter { it >= 0 }.toSet())
    }

    @Test
    fun frameAt_ShouldKeepValuesInRange_WhenSampledAcrossSequence() {
        for (ms in 0L..LaunchTimeline.TOTAL_MS step 10) {
            val f = LaunchTimeline.frameAt(ms)
            listOf(f.gridAlpha, f.bracketProgress, f.sweepAlpha, f.headFlash, f.markLift, f.taglineAlpha, f.taglineSlide, f.readoutAlpha)
                .forEach { assertTrue("value $it out of range at $ms ms", it in 0f..1f) }
            assertTrue(f.sweepAngle in 0f..360f)
            f.pings.forEach { assertTrue(it.radius in 0f..1f && it.alpha in 0f..1f) }
        }
    }

    @Test
    fun finalFrame_ShouldShowEverythingWithoutMotion_WhenRequested() {
        val frame = LaunchTimeline.finalFrame()

        assertEquals(LaunchTimeline.DEFAULT_TITLE_LENGTH, frame.titleChars)
        assertEquals(1f, frame.taglineAlpha, 0.0001f)
        assertEquals(0f, frame.taglineSlide, 0.0001f)
        assertEquals(1f, frame.markLift, 0.0001f)
        assertEquals(LaunchTimeline.MARK_END_SCALE, frame.markScale, 0.0001f)
        assertEquals(LaunchTimeline.STATUS_COUNT - 1, frame.statusIndex)
        assertEquals(1f, frame.bracketProgress, 0.0001f)
        assertEquals(0f, frame.sweepAlpha, 0.0001f)
        assertEquals(0f, frame.headFlash, 0.0001f)
        assertTrue(frame.pings.isEmpty())
        assertFalse(frame.cursorVisible)
        assertTrue(frame.isFinished)
    }

    @Test
    fun frameAt_ShouldMatchFinalFrame_WhenPastTheEnd() {
        assertEquals(LaunchTimeline.finalFrame(), LaunchTimeline.frameAt(LaunchTimeline.TOTAL_MS + 5_000))
    }

    @Test
    fun frameAt_ShouldTreatNegativeTimeAsStart_WhenClockIsBehind() {
        assertEquals(LaunchTimeline.frameAt(0), LaunchTimeline.frameAt(-50))
    }
}
