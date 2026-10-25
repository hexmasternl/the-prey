package nl.hexmaster.theprey.data.language

import android.content.res.Resources
import androidx.appcompat.app.AppCompatDelegate
import androidx.core.os.LocaleListCompat
import java.util.Locale

/** The app's languages, as the player picks them in Settings. */
enum class LanguageChoice(val tag: String?) {
    /** Follow the device: Dutch on a Dutch device, English otherwise. */
    System(null),
    English("en"),
    Dutch("nl"),
}

/** The languages the app and the Users API support (`User.SupportedLanguages` on the server). */
const val LANGUAGE_ENGLISH = "en"
const val LANGUAGE_DUTCH = "nl"

/** The one language rule: `nl` when [locale] is Dutch, `en` for everything else. */
fun supportedLanguageFor(locale: Locale): String =
    if (locale.language == LANGUAGE_DUTCH) LANGUAGE_DUTCH else LANGUAGE_ENGLISH

/** The app's display language: the player's choice, and the language that results from it. */
interface AppLanguage {
    fun current(): LanguageChoice

    /** Applies [choice] to the whole app right away and keeps it across restarts. */
    fun set(choice: LanguageChoice)

    /** `en` or `nl`: the chosen language, or for [LanguageChoice.System] the device's mapped one. */
    fun effective(): String
}

/** Where the per-app locale lives; an interface so the language rule can be tested without Android. */
interface LocaleSource {
    /** Comma-separated BCP 47 tags of the per-app locales; empty means "follow the device". */
    fun appLocaleTags(): String

    fun setAppLocaleTags(tags: String)

    /** The device (system) language, independent of the per-app locale. */
    fun deviceLocale(): Locale
}

class LocaleAppLanguage(private val source: LocaleSource) : AppLanguage {

    override fun current(): LanguageChoice {
        val first = source.appLocaleTags().split(',').firstOrNull()?.trim().orEmpty()
        if (first.isEmpty()) return LanguageChoice.System
        return when (Locale.forLanguageTag(first).language) {
            LANGUAGE_DUTCH -> LanguageChoice.Dutch
            LANGUAGE_ENGLISH -> LanguageChoice.English
            else -> LanguageChoice.System
        }
    }

    override fun set(choice: LanguageChoice) {
        source.setAppLocaleTags(choice.tag.orEmpty())
    }

    override fun effective(): String = when (current()) {
        LanguageChoice.English -> LANGUAGE_ENGLISH
        LanguageChoice.Dutch -> LANGUAGE_DUTCH
        LanguageChoice.System -> supportedLanguageFor(source.deviceLocale())
    }
}

/**
 * Per-app language through AppCompat: on Android 13+ it is the platform's per-app language (also
 * shown in the system settings); below that AppCompat applies it to every AppCompatActivity and
 * stores it through the AppLocalesMetadataHolderService in the manifest. Main thread only.
 */
class AppCompatLocaleSource : LocaleSource {
    override fun appLocaleTags(): String = AppCompatDelegate.getApplicationLocales().toLanguageTags()

    override fun setAppLocaleTags(tags: String) {
        AppCompatDelegate.setApplicationLocales(LocaleListCompat.forLanguageTags(tags))
    }

    override fun deviceLocale(): Locale = Resources.getSystem().configuration.locales[0] ?: Locale.getDefault()
}
