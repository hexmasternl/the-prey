package nl.hexmaster.theprey.data.language

import org.junit.Assert.assertEquals
import org.junit.Test
import java.util.Locale

class AppLanguageTest {

    private class FakeLocaleSource(var tags: String = "", var device: Locale = Locale.ENGLISH) : LocaleSource {
        override fun appLocaleTags() = tags
        override fun setAppLocaleTags(tags: String) {
            this.tags = tags
        }
        override fun deviceLocale() = device
    }

    @Test
    fun effective_ShouldBeDutch_WhenSystemDefaultOnDutchDevice() {
        val sut = LocaleAppLanguage(FakeLocaleSource(device = Locale.forLanguageTag("nl-NL")))

        assertEquals(LanguageChoice.System, sut.current())
        assertEquals("nl", sut.effective())
    }

    @Test
    fun effective_ShouldBeDutch_WhenSystemDefaultOnBelgianDutchDevice() {
        val sut = LocaleAppLanguage(FakeLocaleSource(device = Locale.forLanguageTag("nl-BE")))

        assertEquals("nl", sut.effective())
    }

    @Test
    fun effective_ShouldBeEnglish_WhenSystemDefaultOnGermanDevice() {
        val sut = LocaleAppLanguage(FakeLocaleSource(device = Locale.GERMANY))

        assertEquals("en", sut.effective())
    }

    @Test
    fun effective_ShouldBeEnglish_WhenEnglishIsChosenOnDutchDevice() {
        val source = FakeLocaleSource(device = Locale.forLanguageTag("nl-NL"))
        val sut = LocaleAppLanguage(source)

        sut.set(LanguageChoice.English)

        assertEquals("en", source.tags)
        assertEquals(LanguageChoice.English, sut.current())
        assertEquals("en", sut.effective())
    }

    @Test
    fun effective_ShouldBeDutch_WhenDutchIsChosenOnGermanDevice() {
        val sut = LocaleAppLanguage(FakeLocaleSource(device = Locale.GERMANY))

        sut.set(LanguageChoice.Dutch)

        assertEquals(LanguageChoice.Dutch, sut.current())
        assertEquals("nl", sut.effective())
    }

    @Test
    fun set_ShouldClearTheAppLocale_WhenSystemDefaultIsChosen() {
        val source = FakeLocaleSource(tags = "nl", device = Locale.ENGLISH)
        val sut = LocaleAppLanguage(source)

        sut.set(LanguageChoice.System)

        assertEquals("", source.tags)
        assertEquals(LanguageChoice.System, sut.current())
        assertEquals("en", sut.effective())
    }

    @Test
    fun current_ShouldReadRegionalTags_WhenSetFromSystemSettings() {
        // Android 13's per-app language screen stores a regional tag.
        val sut = LocaleAppLanguage(FakeLocaleSource(tags = "nl-BE,en-GB"))

        assertEquals(LanguageChoice.Dutch, sut.current())
    }

    @Test
    fun supportedLanguageFor_ShouldMapOnlyDutchToDutch() {
        assertEquals("nl", supportedLanguageFor(Locale.forLanguageTag("nl")))
        assertEquals("en", supportedLanguageFor(Locale.FRANCE))
        assertEquals("en", supportedLanguageFor(Locale.US))
    }
}
