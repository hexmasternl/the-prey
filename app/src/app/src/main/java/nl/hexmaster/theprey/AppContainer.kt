package nl.hexmaster.theprey

import android.content.Context
import androidx.datastore.preferences.preferencesDataStore
import com.auth0.android.Auth0
import com.auth0.android.authentication.AuthenticationAPIClient
import com.auth0.android.authentication.storage.SecureCredentialsManager
import com.auth0.android.authentication.storage.SharedPreferencesStorage
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.Dispatchers
import kotlinx.serialization.json.Json
import nl.hexmaster.theprey.auth.Auth0CredentialsStore
import nl.hexmaster.theprey.auth.Auth0WebAuthClient
import nl.hexmaster.theprey.auth.AuthInterceptor
import nl.hexmaster.theprey.auth.SessionManager
import nl.hexmaster.theprey.auth.TokenAuthenticator
import nl.hexmaster.theprey.data.location.FusedLocationSource
import nl.hexmaster.theprey.data.location.LocationSource
import nl.hexmaster.theprey.data.user.DataStoreProfileStore
import nl.hexmaster.theprey.data.user.UserApi
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.OkHttpClient
import retrofit2.Retrofit
import retrofit2.converter.kotlinx.serialization.asConverterFactory
import java.util.Locale

// File name is excluded from backups in backup_rules.xml / data_extraction_rules.xml.
private val Context.profileDataStore by preferencesDataStore(name = "profile")

/** Hand-wired app-wide dependencies. Created once by [ThePreyApplication]. */
class AppContainer(context: Context) {

    /** Lives as long as the process; for work that must outlive a screen. */
    val appScope = CoroutineScope(SupervisorJob() + Dispatchers.Main.immediate)

    private val auth0 = Auth0.getInstance(BuildConfig.AUTH0_CLIENT_ID, BuildConfig.AUTH0_DOMAIN)

    private val credentialsStore = Auth0CredentialsStore(
        SecureCredentialsManager(
            AuthenticationAPIClient(auth0),
            context,
            SharedPreferencesStorage(context),
        ),
    )

    private val json = Json { ignoreUnknownKeys = true }

    // The interceptor and authenticator need the session manager, which needs the API: break the cycle lazily.
    private val onSessionLost: () -> Unit = { sessionManager.onSessionLost() }

    private val httpClient = OkHttpClient.Builder()
        .addInterceptor(AuthInterceptor(credentialsStore, onSessionLost))
        .authenticator(TokenAuthenticator(credentialsStore, onSessionLost))
        .build()

    private val retrofit = Retrofit.Builder()
        .baseUrl(BuildConfig.API_BASE_URL)
        .client(httpClient)
        .addConverterFactory(json.asConverterFactory("application/json".toMediaType()))
        .build()

    val sessionManager: SessionManager = SessionManager(
        credentials = credentialsStore,
        webAuth = Auth0WebAuthClient(auth0, BuildConfig.AUTH0_SCHEME, BuildConfig.AUTH0_AUDIENCE),
        userApi = retrofit.create(UserApi::class.java),
        profileStore = DataStoreProfileStore(context.profileDataStore, json),
        scope = appScope,
        language = { if (Locale.getDefault().language == "nl") "nl" else "en" },
    )

    val locationSource: LocationSource = FusedLocationSource(context)
}
