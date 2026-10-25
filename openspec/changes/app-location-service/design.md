# Design

## Context

- The Android app (`app/src`) has auth (`SessionManager`), a Retrofit/OkHttp stack with token refresh in `AppContainer`, and a `FusedLocationSource` (balanced power, 10 s) used by the main menu readout. There are no game screens and no Game State Service yet. Both come with later changes.
- Permissions and disclosure are owned by `app-location-foreground-service-warning`. It adds `POST_NOTIFICATIONS` and leaves the foreground service declarations to this change.
- The server already has what the tracker needs:
  - `POST /games/{id}/locations` (`RecordLocationRequest(latitude, longitude, recordedAt?, accuracy?)`) for hunter and prey. It rejects with a validation problem (400) unless the game is `InProgress` and the caller is a participant, and returns 404 for an unknown game.
  - `GET /games/active` returns the caller's `Started`/`InProgress` game, or 404 if there is none.
  - `GET /games/{id}/gamestate` works in every status and carries `status`, `endsAt`, the caller's participant state and `serverTime`.
  - The sweep marks a prey `Out` after 7 minutes of GPS silence and penalizes boundary violations from the location history.
- Platform constraints: `minSdk` 26, `targetSdk` 37.
  - A `location` foreground service needs the `FOREGROUND_SERVICE_LOCATION` permission and a granted location permission when it starts.
  - On Android 14+ it must be started while the app has a visible activity, or from a notification tap. A `BOOT_COMPLETED` receiver cannot start it, not even with `ACCESS_BACKGROUND_LOCATION`.
  - A foreground service started while the app is visible keeps location access after the app goes to the background.

## Goals / Non-Goals

**Goals:**
- One authority in the app decides whether tracking may run, so "only during an active game" is enforced in one place and can be unit tested without Android.
- Several independent stop signals, so a missed realtime event or a dead network can never keep the tracker alive past the game.
- Survive backgrounding, swipe-away, screen off and process death. Recover after a reboot with one tap.

**Non-Goals:**
- Detecting mock locations or rooted devices. The server has no field for it yet; this is a later change.
- Boundary checks on the device. The server stays authoritative and the device only reports.
- Opening a realtime socket from the service. The single Game State Service owns the socket (`the-prey-realtime-communication`). The tracker only *receives* its `game-ended` signal.
- Declaring or requesting `ACCESS_BACKGROUND_LOCATION`. The service itself does not need it (see D2), but `app-play-now-button-behavior` already declares it and requires "Allow all the time" before the main menu lets the player start or resume a game, so it is normally granted when tracking starts. That change owns the permission, the settings flow and the Play background-location declaration.

## Decisions

### D1. Split: pure `TrackingController` + thin `LocationTrackingService`

`tracking/TrackingController` is plain Kotlin with injected `GamesApi` (the `data/game/GamesApi` from `app-play-now-button-behavior`, extended with the `locations` and `gamestate` calls), `LocationSource`, `TrackingSessionStore`, a clock and a coroutine scope. It owns the state machine `Idle → Validating → Tracking(gameId, deadline) → Stopped`, the 5-second ticker, the buffer and every stop rule. `LocationTrackingService` (Android `Service`) only promotes itself to the foreground, renders the notification from the controller's state flow, holds the wake lock and calls `stopSelf()` when the controller reaches `Stopped`.
*Why:* the rules that matter ("never run outside a game", "always stop") become JVM unit tests with a fake clock, matching the existing `app/src/app/src/test` setup.
*Alternative:* all logic inside the service. Rejected: it can only be tested on a device.

### D2. Start only from a foreground context, through one entry point

`TrackingStarter.startIfActive(gameId?)` is the only way in. It checks permissions, validates against the server (`/games/active`, or `/gamestate` for a given id) and then calls `ContextCompat.startForegroundService`. Callers:
- app start after `SessionManager` reports signed-in (`ThePreyApplication`, observed in `MainActivity` so an activity is visible);
- the future game screen when the state becomes `Started`/`InProgress`;
- the resume notification tap, which opens `MainActivity` with an extra.

The service rejects (and stops at once) any start intent without a validated session in the store.
*Alternative:* start straight from the realtime `state-changed` handler. Rejected: that handler can run while no activity is visible, and the start would throw `ForegroundServiceStartNotAllowedException`.

### D3. Persisted tracking session

`TrackingSessionStore` is a DataStore file `tracking`, excluded from backups like `profile`. It holds `{gameId, deadline, stopped}`. It is written when tracking is validated, updated when `endsAt` becomes known, and cleared on stop or sign-out. It lets the boot receiver and a system restart (`START_STICKY`) find the game without the network. `stopped = true` blocks any self-restart for that game, which covers "never restarts itself".

### D4. Deadline = `endsAt` + 2 min, measured against the server clock

On validation the controller reads `/gamestate` and computes `offset = serverTime − deviceNow`, so a wrong device clock cannot stretch the deadline.
- `InProgress`: deadline = `endsAt + 2 min`.
- `Started` (no `endsAt` yet): deadline = `now + gameDuration + 2 min`, and `/gamestate` is re-read every 10 seconds until `endsAt` is known. That takes at most about 30 s, one sweep.

The deadline is enforced by a coroutine `delay` in the controller and, as a second guard, an exact-free `AlarmManager.set` alarm (no `SCHEDULE_EXACT_ALARM` needed) that sends a stop intent in case the process was frozen.

### D5. Stop signals (any one wins)

| Signal | Source |
|---|---|
| `keepReporting == false` | location ack (new server field) |
| post rejected with 400/403/404 | confirmed by one `/gamestate` read, at most once per 30 s: stop if `Completed`, 404, 403 or the caller is `Tagged`/`Out`; keep going if `Started` |
| `game-ended` | `TrackingController.onGameEnded(gameId)`, the hook the Game State Service calls when it exists |
| deadline | D4 |
| sign-out | controller observes `SessionManager` state |

A 401 is left to the existing `TokenAuthenticator`. If the session is lost, that is a sign-out.

### D6. Fixed 5-second ticker, decoupled from fixes

`FusedLocationSource` gets a `highAccuracy(intervalMs = 5000)` variant: `PRIORITY_HIGH_ACCURACY`, `minUpdateIntervalMillis` 5000, `waitForAccurateLocation` false. The controller keeps the latest fix and a ticker posts it every 5 s if it is newer than the last posted one. The ticker is anchored to the tracking start, not to the end of the previous post, so slow posts do not cause drift. Posts are sequential: one in flight at a time, with the buffer draining before the new reading.
*Alternative:* post on every fix callback. Rejected: fix timing varies and could flood the server or go quiet.

### D7. Bounded in-memory buffer

The buffer is an `ArrayDeque` capped at 120 readings (10 minutes at 5 s). Network errors, timeouts and 5xx responses push the reading back, and the next tick drains oldest first, sending at most 12 readings per tick so the server never gets a burst. The buffer lives in memory only. After process death the readings are lost, which is fine because the 7-minute silence rule is the safety net anyway.

### D8. Keeping the process and network alive

- Service declared with `android:foregroundServiceType="location"`, `android:exported="false"`, `onStartCommand` returns `START_STICKY`.
- `onTaskRemoved` keeps running.
- A partial wake lock is held with a timeout equal to the time left until the deadline, and released on stop.
- The notification uses `setOngoing(true)` and `FOREGROUND_SERVICE_IMMEDIATE`, with a low-importance channel `game_tracking` (no sound). Its countdown is a chronometer counting down to the deadline.
- No battery-optimization exemption is requested (Play policy). Foreground service processes keep network access in Doze.

### D9. Reboot / update

`TrackingBootReceiver` listens for `BOOT_COMPLETED` and `MY_PACKAGE_REPLACED`. It reads the store. If the session is unexpired and not stopped, it posts a high-importance notification on channel `game_resume`, which opens `MainActivity` with `EXTRA_RESUME_TRACKING`. If the session expired, it clears the store. `MainActivity` calls `TrackingStarter.startIfActive(gameId)` on that extra, and the resume notification is cancelled when tracking starts.

### D10. Server: `KeepReporting` + interval 5

`RecordLocationResponse` gets `bool KeepReporting = true`. The trailing default keeps the positional record and existing JSON clients compatible. The handler sets it from the participant's state (`Tagged`/`Out` → false). `Game.LocationReportingIntervalSeconds` becomes 5. Tests are updated in `RecordPlayerLocationCommandHandlerTests`. OTel is already in place in that handler; it gets one low-cardinality tag, `game.keep_reporting`.

## Risks / Trade-offs

- [Player kills the service via "Force stop" or revokes the permission mid-game] → Nothing on the device can prevent it. The server sees silence and applies the existing Passive (5 min) / Out (7 min) rules. The notification warns about exactly this.
- [Reboot needs a tap] → The resume notification is high priority and the silence rules give the player an incentive. This is a platform limit, not a choice.
- [Battery drain from high-accuracy GPS plus a post every 5 s] → Bounded by the game duration and stopped promptly by D5. Measure on a device during implementation, and note in the tasks the option to drop to balanced accuracy when the device is stationary.
- [Started-state posts get 400 for up to ~30 s] → Expected. Confirmation reads are rate-limited (D5) and the Started deadline is bounded (D4).
- [OEM battery killers (Xiaomi, Huawei, Samsung "sleeping apps") stop foreground services] → `START_STICKY` plus re-validation on restart. The disclosure/help text can point to dontkillmyapp.com later. Not solved here.
- [Game State Service not yet built] → The `onGameEnded` hook is defined now. Until that service is wired in, `keepReporting`, the 400/404 confirmation and the deadline still stop tracking within 5 s of the server noticing.

## Migration Plan

1. Ship the server change first. It is additive, so old clients ignore `keepReporting`.
2. Ship the app with the service. No data migration; the new DataStore file starts empty.
3. Rollback: removing the service from the app is safe. The server change can stay.

## Open Questions

- The exact wording of the notification texts (EN/NL), settled with the `the-prey-multilingual` skill during implementation.
- Whether to log tracking start/stop/stop-reason to the server for support. Deferred; it does not change behavior.
