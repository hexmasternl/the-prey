## Context

The MAUI app currently has `MauiGpsReader` (`Services/Location/`), a one-shot `IGpsReader` using MAUI `IGeolocation` with **when-in-use** permission, built for the decorative HUD readout. It returns `null` on any failure and stops the moment the app leaves the foreground.

An in-progress game requires the opposite characteristics: continuous position reporting for the whole game, surviving backgrounding and screen lock. The backend already accepts these reports at `POST /games/{id}/locations` (`RecordLocationRequest { Latitude, Longitude, RecordedAt?, Accuracy? }`, bearer-authenticated, user resolved from the `sub` claim). The response `RecordLocationResponse { Accepted, NextLocationIntervalSeconds, PenaltyIntervalSeconds?, PenaltyEndsAt? }` dictates the next cadence — the server owns the ping interval (see `server-calculated-ping-interval`). The backend `PlayerStateMonitor` (`player-status-tracking`) flips a prey to `Passive` after 5 min of silence and `Out` after 7 min, so sustained silence is game-losing.

MAUI has **no** cross-platform background-execution or background-location primitive. This must be built per-platform behind a shared abstraction, consistent with the app's existing "platform adapter behind an interface" pattern (`IGpsReader`, `IApplicationExit`, `IMenuNavigator`).

## Goals / Non-Goals

**Goals:**
- One DI-registered `IGameLocationTracker` that view models start/stop without touching platform code.
- Continuous reporting to `POST /games/{id}/locations` for the full InProgress game, surviving background + screen-lock on Android and iOS.
- Server-driven cadence, seeded at 10 s, adopting `NextLocationIntervalSeconds`/penalty interval per response.
- Automatic stop on game-end (notification or endpoint game-over signal), releasing wake-lock/notification.
- Resilience: transient GPS/network failures never stop the game; graceful degradation when background permission is denied.
- Unit tests for the shared coordinator (cadence, adoption, retry, lifecycle).

**Non-Goals:**
- Backend changes — the endpoint and contract already exist.
- Windows / MacCatalyst background tracking beyond a foreground fallback (field play is phone-only; desktop targets report only while active).
- Real-time consumption of other players' positions (SSE/Web PubSub receive path is a separate concern).
- Batching/offline queueing of missed fixes across long outages (out of scope for v1; each tick reports the current fix only).

## Decisions

### 1. Shared coordinator + thin native adapters
A platform-agnostic `GameLocationTrackerCoordinator` owns all logic: the cadence loop, token acquisition, HTTP report, cadence adoption, retry, and stop conditions. Each platform provides only two thin adapters behind interfaces:
- `IBackgroundExecutionHost` — start/stop the OS mechanism that keeps the process alive (Android foreground service; iOS `allowsBackgroundLocationUpdates`).
- `IContinuousLocationSource` — deliver position fixes (or the coordinator pulls on a timer where the platform prefers that).

`IGameLocationTracker` (the public façade) delegates to the coordinator. **Why:** concentrates all testable logic in one platform-neutral class; native code stays minimal and adapter-shaped, matching the existing `IGpsReader` convention. **Alternative rejected:** a full third-party library (Shiny.NET) — powerful but a heavy new dependency that owns lifecycle/DI in ways that conflict with the app's hand-rolled patterns; revisit only if native maintenance proves costly.

### 2. Android — Foreground Service (type `location`)
A bound/started `Android.App.Service` with `foregroundServiceType="location"`, a dedicated low-importance notification channel, and a persistent notification. It holds the process alive; the coordinator's loop runs while it is up. **Why:** the only Android-sanctioned way to run location work with the screen off without Doze killing it. Requires `ACCESS_BACKGROUND_LOCATION`, `FOREGROUND_SERVICE`, `FOREGROUND_SERVICE_LOCATION`, `POST_NOTIFICATIONS` and a `<service>` manifest entry. The persistent notification is mandatory and cannot be hidden.

### 3. iOS — continuous CLLocationManager background updates
`CLLocationManager` with `AllowsBackgroundLocationUpdates = true`, `PausesLocationUpdatesAutomatically = false`, `UIBackgroundModes: [location]`, and **Always** authorization. Reports are sent from the `LocationsUpdated` callback. **Why:** iOS suspends apps; active location updates are the sanctioned way to keep running in the background. **Consequence:** exact 10 s wall-clock cadence is not guaranteed — cadence is approximated by keeping accuracy/distance filter tight and gating sends to at most one per adopted interval. The blue status-bar indicator shows, and "Always" needs an App Store privacy justification.

### 4. Server-driven cadence, 10 s seed, clamped
The coordinator holds a `currentInterval`, seeded at 10 s. After each accepted report it adopts `NextLocationIntervalSeconds` (and penalty interval when active), clamped to a minimum (e.g. 5 s) so a zero/negative value can't busy-loop. **Why:** honours the existing server-calculated ping interval design while satisfying the "every 10 s" default the game starts at.

### 5. Lifecycle wiring — start at InProgress, stop on game-over
Start is triggered when the app resolves/enters an InProgress game (game start flow / `SessionService` result). Stop is triggered by: (a) the game-ended notification the app already receives, and (b) defensively, a location report returning a game-not-InProgress signal (404/422) for the tracked game. On stop the coordinator cancels the loop and tears down the native host. **Why:** two independent stop signals guard against a missed push notification leaving GPS running (battery/privacy) after the game ends.

### 6. Reuse existing auth + a dedicated report client
Reporting reuses the established token path (`ITokenStore` + `IAuth0TokenClient` refresh, as in `SessionService`) and a typed `HttpClient` report client analogous to `GameApiClient`, pointed at `BackendBaseUrl`. **Why:** consistent with existing session/token handling; no new auth surface.

## Risks / Trade-offs

- **iOS cadence is approximate** → Gate sends to one per adopted interval and keep the distance filter tight; accept that a stationary player may report slightly less often than 10 s. The backend's 5-/7-minute silence thresholds give ample margin.
- **Battery drain during a game** → Intentional and disclosed via the Android notification / iOS indicator; scoped strictly to InProgress via the dual stop signals so it never runs outside a game.
- **App Store rejection for "Always" location** → Mitigate with a clear `NSLocationAlwaysAndWhenInUseUsageDescription` and privacy-manifest justification framing it as core gameplay.
- **Background permission denied** → Degrade to foreground-only reporting rather than blocking play; the player is warned they'll go `Passive` if they pocket the phone.
- **Missed game-ended push leaves tracker running** → Defensive endpoint-driven stop (404/422) and a max-lifetime safety cap tied to the game's configured duration.
- **OS kills the process (task-swipe, low memory)** → Accepted for v1; the foreground service resists this on Android, and re-entering the app re-establishes tracking if the game is still InProgress.

## Migration Plan

Additive feature — no data migration. Ship behind the game-start flow; if the tracker fails to start, the game still runs (foreground-only fallback). Rollback = don't invoke start (revert wiring); no persisted state to unwind.

## Open Questions

- Exact start trigger: on `StartGame` success only, or also on cold-start resume into an InProgress game via `SessionService`? (Leaning: both.)
- Should the Android notification expose a "stop / leave game" action, or is it purely informational?
- Do we need a minimum-movement distance filter to save battery, or does the game require fixed-cadence pings even when stationary? (Backend `NextLocationIntervalSeconds` suggests time-based; confirm.)
