package nl.hexmaster.theprey

import android.app.Notification
import android.app.NotificationChannel
import android.app.NotificationManager
import android.app.Service
import android.content.Context
import android.content.Intent
import android.location.Location
import android.os.Handler
import android.os.IBinder
import android.os.Looper
import android.util.Log
import androidx.core.app.NotificationCompat
import com.google.android.gms.location.FusedLocationProviderClient
import com.google.android.gms.location.LocationCallback
import com.google.android.gms.location.LocationRequest
import com.google.android.gms.location.LocationResult
import com.google.android.gms.location.LocationServices
import com.google.android.gms.location.Priority
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.RequestBody.Companion.toRequestBody
import okhttp3.Response
import org.json.JSONObject
import java.io.IOException
import java.util.concurrent.TimeUnit

/**
 * Foreground service that continuously tracks the prey's GPS location and POSTs it
 * to the game API. Survives screen lock because it holds a foreground service
 * notification; FusedLocationProviderClient handles its own wakelock internally.
 *
 * The service is autonomous: each tick it polls the game status endpoint, updates
 * its own interval from [nextPingDuration], and self-terminates when the game ends.
 *
 * Start it via [GameLocationPlugin.startTracking]; stop it via [GameLocationPlugin.stopTracking]
 * or by sending ACTION_STOP as an explicit intent.
 *
 * Intent extras (all required on ACTION_START):
 *   gameId       – UUID string of the active game
 *   apiUrl       – base URL (no trailing slash), e.g. "https://api.theprey.nl"
 *   clientId     – Auth0 machine-to-machine client ID
 *   clientSecret – Auth0 machine-to-machine client secret
 *   userId       – the player's own user ID (used to detect elimination)
 *   intervalMs   – initial polling interval in milliseconds
 */
class LocationForegroundService : Service() {

    companion object {
        const val ACTION_START = "nl.hexmaster.theprey.ACTION_START_TRACKING"
        const val ACTION_STOP  = "nl.hexmaster.theprey.ACTION_STOP_TRACKING"
        const val ACTION_UPDATE_INTERVAL = "nl.hexmaster.theprey.ACTION_UPDATE_INTERVAL"

        const val EXTRA_GAME_ID       = "gameId"
        const val EXTRA_API_URL       = "apiUrl"
        const val EXTRA_CLIENT_ID     = "clientId"
        const val EXTRA_CLIENT_SECRET = "clientSecret"
        const val EXTRA_USER_ID       = "userId"
        const val EXTRA_INTERVAL_MS   = "intervalMs"

        private const val TOKEN_URL  = "https://theprey.eu.auth0.com/oauth/token"
        private const val AUDIENCE   = "https://api.theprey.nl"

        private const val NOTIFICATION_ID      = 1001
        private const val CHANNEL_ID           = "location_tracking"
        private const val CHANNEL_NAME         = "Location Tracking"
        private const val TAG                  = "LocationFgService"
        private const val DEFAULT_INTERVAL_MS  = 30_000L
    }

    // -------------------------------------------------------------------------
    // State
    // -------------------------------------------------------------------------

    private lateinit var fusedLocationClient: FusedLocationProviderClient
    private lateinit var httpClient: OkHttpClient

    private var gameId: String        = ""
    private var apiUrl: String        = ""
    private var clientId: String      = ""
    private var clientSecret: String  = ""
    private var userId: String        = ""
    private var intervalMs: Long      = DEFAULT_INTERVAL_MS

    @Volatile private var cachedToken: String  = ""
    @Volatile private var tokenExpiresAt: Long = 0L  // epoch milliseconds

    /** Last known location — updated by FusedLocationProviderClient callback. */
    @Volatile private var lastLocation: Location? = null

    /**
     * Single tick runnable: checks game status (blocking, background thread),
     * posts location if the game is still active, then schedules the next tick.
     * Everything runs on a plain Thread so OkHttp execute() is safe to call.
     */
    private val handler = Handler(Looper.getMainLooper())
    private val tickRunnable = object : Runnable {
        override fun run() {
            Thread {
                val shouldContinue = checkGameStatus()
                if (shouldContinue) {
                    lastLocation?.let { postLocation(it) }
                }
                if (shouldContinue) {
                    handler.postDelayed(this, intervalMs)
                }
            }.start()
        }
    }

    private val locationCallback = object : LocationCallback() {
        override fun onLocationResult(result: LocationResult) {
            lastLocation = result.lastLocation
        }
    }

    // -------------------------------------------------------------------------
    // Lifecycle
    // -------------------------------------------------------------------------

    override fun onCreate() {
        super.onCreate()
        fusedLocationClient = LocationServices.getFusedLocationProviderClient(this)
        httpClient = OkHttpClient.Builder()
            .connectTimeout(10, TimeUnit.SECONDS)
            .readTimeout(10, TimeUnit.SECONDS)
            .writeTimeout(10, TimeUnit.SECONDS)
            .build()
        createNotificationChannel()
    }

    override fun onStartCommand(intent: Intent?, flags: Int, startId: Int): Int {
        when (intent?.action) {
            ACTION_STOP -> {
                stopTracking()
                return START_NOT_STICKY
            }
            ACTION_UPDATE_INTERVAL -> {
                intervalMs = intent.getLongExtra(EXTRA_INTERVAL_MS, intervalMs)
                Log.d(TAG, "Interval updated to ${intervalMs}ms")
                return START_STICKY
            }
            ACTION_START, null -> {
                // Accept null action so the service can be restarted by the system
                // (START_STICKY) with the last intent. Extract extras only when present.
                intent?.let { applyExtras(it) }
                startTracking()
            }
        }
        return START_STICKY
    }

    override fun onBind(intent: Intent?): IBinder? = null

    override fun onDestroy() {
        stopTracking()
        super.onDestroy()
    }

    // -------------------------------------------------------------------------
    // Internal helpers
    // -------------------------------------------------------------------------

    private fun applyExtras(intent: Intent) {
        gameId       = intent.getStringExtra(EXTRA_GAME_ID)       ?: gameId
        apiUrl       = intent.getStringExtra(EXTRA_API_URL)       ?: apiUrl
        clientId     = intent.getStringExtra(EXTRA_CLIENT_ID)     ?: clientId
        clientSecret = intent.getStringExtra(EXTRA_CLIENT_SECRET) ?: clientSecret
        userId       = intent.getStringExtra(EXTRA_USER_ID)       ?: userId
        intervalMs   = intent.getLongExtra(EXTRA_INTERVAL_MS, intervalMs)
    }

    private fun startTracking() {
        if (gameId.isEmpty() || apiUrl.isEmpty() || clientId.isEmpty() || clientSecret.isEmpty() || userId.isEmpty()) {
            Log.e(TAG, "Missing required extras — cannot start tracking")
            stopSelf()
            return
        }

        startForeground(NOTIFICATION_ID, buildNotification())
        requestLocationUpdates()
        handler.postDelayed(tickRunnable, intervalMs)
        Log.d(TAG, "Location tracking started for game $gameId, interval ${intervalMs}ms")
    }

    private fun stopTracking() {
        // removeCallbacks and stopForeground/stopSelf must all run on the main thread.
        // checkGameStatus() calls this from a background Thread, so post to the handler.
        handler.post {
            handler.removeCallbacks(tickRunnable)
            fusedLocationClient.removeLocationUpdates(locationCallback)
            stopForeground(STOP_FOREGROUND_REMOVE)
            stopSelf()
            Log.d(TAG, "Location tracking stopped")
        }
    }

    @Suppress("MissingPermission") // Permission checked at plugin layer before service starts
    private fun requestLocationUpdates() {
        val request = LocationRequest.Builder(Priority.PRIORITY_HIGH_ACCURACY, intervalMs)
            .setMinUpdateIntervalMillis(intervalMs / 2)
            .setMaxUpdateDelayMillis(intervalMs)
            .build()

        fusedLocationClient.requestLocationUpdates(request, locationCallback, Looper.getMainLooper())
    }

    /**
     * Returns a valid access token, fetching a new one from Auth0 if the cached token
     * has expired or is within 60 seconds of expiry. Blocking — call from a background thread.
     */
    private fun getValidToken(): String? {
        if (cachedToken.isNotEmpty() && System.currentTimeMillis() < tokenExpiresAt - 60_000) {
            return cachedToken
        }
        return fetchAccessToken()
    }

    /**
     * Fetches a new access token from Auth0 using the client credentials grant.
     * Blocking — must be called from a background thread.
     */
    private fun fetchAccessToken(): String? {
        val body = JSONObject().apply {
            put("client_id",     clientId)
            put("client_secret", clientSecret)
            put("audience",      AUDIENCE)
            put("grant_type",    "client_credentials")
        }.toString()

        val request = Request.Builder()
            .url(TOKEN_URL)
            .addHeader("Content-Type", "application/json")
            .post(body.toRequestBody("application/json".toMediaType()))
            .build()

        return try {
            httpClient.newCall(request).execute().use { response ->
                if (!response.isSuccessful) {
                    Log.w(TAG, "Auth0 token request failed — HTTP ${response.code}")
                    return null
                }
                val json = JSONObject(response.body?.string() ?: return null)
                val token = json.getString("access_token")
                tokenExpiresAt = parseJwtExpiry(token)
                cachedToken    = token
                token
            }
        } catch (e: IOException) {
            Log.w(TAG, "Auth0 token request failed (network error): ${e.message}")
            null
        }
    }

    /**
     * Decodes the `exp` claim from a JWT payload segment and returns it as epoch milliseconds.
     */
    private fun parseJwtExpiry(jwt: String): Long {
        return try {
            val parts = jwt.split(".")
            if (parts.size < 2) return 0L
            val payload = String(android.util.Base64.decode(
                parts[1], android.util.Base64.URL_SAFE or android.util.Base64.NO_WRAP or android.util.Base64.NO_PADDING
            ))
            JSONObject(payload).getLong("exp") * 1000L
        } catch (e: Exception) {
            0L
        }
    }

    /**
     * Polls the game status endpoint synchronously (must be called from a background thread).
     *
     * Returns true if the game is still active and tracking should continue.
     * Returns false when the game is over, causing the service to self-terminate.
     *
     * Side-effect: updates [intervalMs] from the server-provided [nextPingDuration].
     */
    private fun checkGameStatus(): Boolean {
        val token = getValidToken() ?: run {
            Log.w(TAG, "Could not obtain access token — skipping status check")
            return true  // treat as a transient error, retry next tick
        }

        val request = Request.Builder()
            .url("$apiUrl/games/$gameId/status")
            .addHeader("Authorization", "Bearer $token")
            .get()
            .build()

        return try {
            httpClient.newCall(request).execute().use { response ->
                when (response.code) {
                    404, 410 -> {
                        Log.i(TAG, "Game $gameId no longer exists (HTTP ${response.code}) — stopping")
                        stopTracking()
                        false
                    }
                    !in 200..299 -> {
                        // Non-terminal error (e.g. 500, network blip) — skip tick, retry later
                        Log.w(TAG, "Game status check HTTP ${response.code} — will retry next tick")
                        true
                    }
                    else -> {
                        val body = response.body?.string() ?: return@use true
                        val json = JSONObject(body)

                        val gameDurationLeft = json.optInt("gameDurationLeft", 1)
                        val nextPingDuration = json.optInt("nextPingDuration", (intervalMs / 1000).toInt())
                        intervalMs = nextPingDuration * 1000L

                        if (gameDurationLeft <= 0) {
                            Log.i(TAG, "Game $gameId ended (gameDurationLeft=$gameDurationLeft) — stopping")
                            stopTracking()
                            return@use false
                        }

                        // Check whether this player is still a participant
                        val hunter = json.optJSONObject("hunter")
                        val hunterUserId = hunter?.optString("userId", "") ?: ""
                        val preys = json.optJSONArray("preys")

                        val isHunter = hunterUserId == userId
                        var isPrey = false
                        if (preys != null) {
                            for (i in 0 until preys.length()) {
                                val prey = preys.optJSONObject(i)
                                if (prey?.optString("userId", "") == userId) {
                                    isPrey = true
                                    break
                                }
                            }
                        }

                        if (!isHunter && !isPrey) {
                            Log.i(TAG, "Player $userId is no longer a participant — stopping")
                            stopTracking()
                            return@use false
                        }

                        Log.d(TAG, "Game active — ${gameDurationLeft}s left, next ping in ${nextPingDuration}s")
                        true
                    }
                }
            }
        } catch (e: IOException) {
            // Network unavailable — skip this tick, retry on next interval
            Log.w(TAG, "Game status check failed (network error): ${e.message}")
            true
        }
    }

    /**
     * POSTs the current GPS location to the game API synchronously
     * (must be called from a background thread).
     */
    private fun postLocation(location: Location) {
        val token = getValidToken() ?: run {
            Log.w(TAG, "Could not obtain access token — skipping location post")
            return
        }

        val url = "$apiUrl/games/$gameId/location"
        val body = JSONObject().apply {
            put("latitude",  location.latitude)
            put("longitude", location.longitude)
        }.toString()

        val request = Request.Builder()
            .url(url)
            .addHeader("Authorization", "Bearer $token")
            .addHeader("Content-Type",  "application/json")
            .post(body.toRequestBody("application/json".toMediaType()))
            .build()

        try {
            httpClient.newCall(request).execute().use { response ->
                if (!response.isSuccessful) {
                    Log.w(TAG, "POST location HTTP ${response.code}")
                } else {
                    Log.d(TAG, "Location posted: ${location.latitude}, ${location.longitude}")
                }
            }
        } catch (e: IOException) {
            // Network unavailable — skip this interval; the handler will retry next tick
            Log.w(TAG, "POST location failed (network error): ${e.message}")
        }
    }

    // -------------------------------------------------------------------------
    // Notification
    // -------------------------------------------------------------------------

    private fun createNotificationChannel() {
        val channel = NotificationChannel(
            CHANNEL_ID,
            CHANNEL_NAME,
            NotificationManager.IMPORTANCE_LOW // Low importance = no sound, no pop-up
        ).apply {
            description = "Shown while location broadcasting is active during a game"
            setShowBadge(false)
        }
        val nm = getSystemService(Context.NOTIFICATION_SERVICE) as NotificationManager
        nm.createNotificationChannel(channel)
    }

    private fun buildNotification(): Notification {
        return NotificationCompat.Builder(this, CHANNEL_ID)
            .setContentTitle("The Prey")
            .setContentText("Location tracking active — you are being hunted")
            .setSmallIcon(R.mipmap.ic_launcher)
            .setOngoing(true)          // Cannot be swiped away
            .setSilent(true)           // No sound on notification update
            .setCategory(NotificationCompat.CATEGORY_SERVICE)
            .setForegroundServiceBehavior(NotificationCompat.FOREGROUND_SERVICE_IMMEDIATE)
            .build()
    }
}
