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
import nl.hexmaster.theprey.data.game.ApiGameRepository
import nl.hexmaster.theprey.data.game.GameRepository
import nl.hexmaster.theprey.data.game.GamesApi
import nl.hexmaster.theprey.data.language.AppCompatLocaleSource
import nl.hexmaster.theprey.data.language.AppLanguage
import nl.hexmaster.theprey.data.language.LocaleAppLanguage
import nl.hexmaster.theprey.data.location.DataStoreLocationConsentStore
import nl.hexmaster.theprey.data.location.FusedLocationSource
import nl.hexmaster.theprey.data.location.LocationConsentStore
import nl.hexmaster.theprey.data.location.LocationSource
import nl.hexmaster.theprey.data.location.readLocationAccess
import nl.hexmaster.theprey.data.location.LocationAccess
import nl.hexmaster.theprey.data.playfield.ApiPlayFieldRepository
import nl.hexmaster.theprey.data.playfield.PlayFieldApi
import nl.hexmaster.theprey.data.playfield.PlayFieldRepository
import nl.hexmaster.theprey.data.user.DataStoreProfileStore
import nl.hexmaster.theprey.data.user.UserApi
import nl.hexmaster.theprey.tracking.DataStoreTrackingSessionStore
import nl.hexmaster.theprey.tracking.LocationTrackingService
import nl.hexmaster.theprey.tracking.TrackingController
import nl.hexmaster.theprey.tracking.TrackingSessionStore
import nl.hexmaster.theprey.tracking.TrackingStarter
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.OkHttpClient
import retrofit2.Retrofit
import retrofit2.converter.kotlinx.serialization.asConverterFactory

// File name is excluded from backups in backup_rules.xml / data_extraction_rules.xml.
private val Context.profileDataStore by preferencesDataStore(name = "profile")
private val Context.locationConsentDataStore by preferencesDataStore(name = "location_consent")
private val Context.trackingDataStore by preferencesDataStore(name = "tracking")

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

    /** The app display language (per-app locale); also the language sent to the Users API. */
    val appLanguage: AppLanguage = LocaleAppLanguage(AppCompatLocaleSource())

    val sessionManager: SessionManager = SessionManager(
        credentials = credentialsStore,
        webAuth = Auth0WebAuthClient(auth0, BuildConfig.AUTH0_SCHEME, BuildConfig.AUTH0_AUDIENCE),
        userApi = retrofit.create(UserApi::class.java),
        profileStore = DataStoreProfileStore(context.profileDataStore, json),
        scope = appScope,
        language = { appLanguage.effective() },
    )

    private val fusedLocationSource = FusedLocationSource(context)

    val locationSource: LocationSource = fusedLocationSource

    /** Device-local: survives sign-out, reset only by clearing app data or reinstalling. */
    val locationConsentStore: LocationConsentStore = DataStoreLocationConsentStore(context.locationConsentDataStore)

    private val gamesApi = retrofit.create(GamesApi::class.java)

    /** The signed-in player's games; shared by the main menu and the in-game location tracking. */
    val gameRepository: GameRepository = ApiGameRepository(gamesApi)

    /** The signed-in player's playfields: list own, search public, load, save, delete. */
    val playFieldRepository: PlayFieldRepository =
        ApiPlayFieldRepository(retrofit.create(PlayFieldApi::class.java), json)

    /** The game the in-game location tracking runs for; read by the service and the boot receiver. */
    val trackingSessionStore: TrackingSessionStore = DataStoreTrackingSessionStore(context.trackingDataStore)

    /** The single authority over in-game location tracking. */
    val trackingController = TrackingController(
        api = gamesApi,
        locations = fusedLocationSource,
        store = trackingSessionStore,
        session = sessionManager.state,
        scope = appScope,
    )

    /** The only entry point that starts tracking; call it from a visible activity. */
    val trackingStarter = TrackingStarter(
        controller = trackingController,
        games = gameRepository,
        hasFineLocation = { readLocationAccess(context) >= LocationAccess.ForegroundOnly },
        launchService = { LocationTrackingService.start(context) },
    )
}
