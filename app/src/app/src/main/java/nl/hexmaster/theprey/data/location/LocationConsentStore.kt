package nl.hexmaster.theprey.data.location

import androidx.datastore.core.DataStore
import androidx.datastore.preferences.core.Preferences
import androidx.datastore.preferences.core.booleanPreferencesKey
import androidx.datastore.preferences.core.edit
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.flow.map

/**
 * Device-local record of the location tracking disclosure. Not tied to an account: it survives
 * sign-out and is reset only by clearing app data or reinstalling.
 */
interface LocationConsentStore {
    /** True once the player tapped Continue in the disclosure (or Grant in Settings) on this device. Defaults to false. */
    val disclosureAccepted: Flow<Boolean>
    suspend fun isDisclosureAccepted(): Boolean = disclosureAccepted.first()
    suspend fun setDisclosureAccepted(accepted: Boolean)

    /**
     * True once the main menu showed the disclosure by itself on this device, whatever the player
     * chose. From then on the main menu never shows it automatically again. Defaults to false.
     */
    val menuPromptShown: Flow<Boolean>
    suspend fun isMenuPromptShown(): Boolean = menuPromptShown.first()
    suspend fun setMenuPromptShown()
}

/** [LocationConsentStore] backed by a Preferences DataStore (app-private, excluded from backups). */
class DataStoreLocationConsentStore(private val dataStore: DataStore<Preferences>) : LocationConsentStore {

    override val disclosureAccepted: Flow<Boolean> = dataStore.data.map { it[DISCLOSURE_ACCEPTED_KEY] ?: false }

    override suspend fun setDisclosureAccepted(accepted: Boolean) {
        dataStore.edit { it[DISCLOSURE_ACCEPTED_KEY] = accepted }
    }

    override val menuPromptShown: Flow<Boolean> = dataStore.data.map { it[MENU_PROMPT_SHOWN_KEY] ?: false }

    override suspend fun setMenuPromptShown() {
        dataStore.edit { it[MENU_PROMPT_SHOWN_KEY] = true }
    }

    private companion object {
        val DISCLOSURE_ACCEPTED_KEY = booleanPreferencesKey("disclosure_accepted")
        val MENU_PROMPT_SHOWN_KEY = booleanPreferencesKey("menu_prompt_shown")
    }
}
