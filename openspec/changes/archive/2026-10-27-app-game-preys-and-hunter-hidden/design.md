# Design

## Context

See proposal.md - Why. The investigation followed the whole location chain:

| Link | Code | Finding |
|---|---|---|
| Device → server | `tracking/TrackingController` posts every 5 s once `start()` succeeded | Correct, but `start()` is only reached through `TrackingStarter.startIfActive` |
| Who calls the starter | `ui/tracking/TrackingAutoStart`, a `LaunchedEffect(signedInUserId, request)` in `ThePreyNavHost` | Runs once per sign-in (app launch, before any game started) and on the resume-notification tap. **Nothing calls it when the lobby navigates to `Routes.GAME`, or when the game screen opens via "Resume game"** |
| Server sweep | `GameSweepProcessor` batches `SweepLocations` broadcasts into one `locations-updated` | Correct; with no readings there is nothing to broadcast |
| Server → group | Dapr topic → `NotificationSubscriptionEndpoints` → `WebPubSubBroadcaster.SendToGroupAsync(gameId)` | Correct group (game id) and envelope. `seq` comes from an in-memory per-replica counter (see D3) |
| Socket + state | `WebPubSubClient` joins the group; `GameStateReducer` overlays `locations-updated` onto `participants[].lastKnownLocation` | Correct (covered by `RealtimeModelsTest` / reducer tests) |
| Rendering | `markersFor` → `MarkerStyle.of` → `GameMap` GeoJSON source | Correct. A participant without `lastKnownLocation` is skipped, so no locations means no markers |

The `app-location-service` design (D2) lists "the game screen when the state becomes `Started`/`InProgress`" as a caller. The `app-game-active-view` design (Non-Goals) says the game screen does not start tracking because `TrackingAutoStart` "already owns that". That gap is the bug. A player who reopens the app mid-game is covered by the app-start path, which is why the bug looks intermittent.

Android 14+ only lets a location foreground service start while an activity is visible. The game screen qualifies, and a realtime handler does not.

## Goals / Non-Goals

**Goals:**
- Tracking starts for every participant as soon as their game screen shows an active game, on every route into that screen.
- Keep `TrackingStarter.startIfActive` the only entry point, with the consent flow unchanged.
- Remove the `seq` interleaving that multiple Notifications replicas would cause.

**Non-Goals:**
- Changing the reporting cadence, the sweep's broadcast schedule, or which roles see whom.
- Starting tracking from the lobby before the game starts (the spec forbids tracking in the lobby).
- A distributed `seq` counter (Redis etc.); a single replica is enough at current load.

## Decisions

### D1. The game route triggers the starter, keyed on game id and phase

The `Routes.GAME` destination in `ThePreyNavHost` hosts a small effect, `GameTrackingStart(starter, gameId, active)`, next to `TrackingAutoStart` in `ui/tracking/`. It calls `starter.startIfActive(gameId)` when the screen's `ActiveGameUi` is `Ready` with the game `Started`/`InProgress` and the player in play. The consent branch (`NeedsConsent` → `LocalLocationConsent.requireLocation`, decline → "silent" toast) is reused through a shared helper extracted from `TrackingAutoStart`. The effect is keyed on `gameId`, so it runs once per game-screen entry, not on every snapshot.

*Why here:* this is the one place every route into the game passes through (lobby `onStarted`, "Resume game", notification → menu → resume), and it is a visible activity. *Alternative:* call the starter from `LobbyViewModel` when it sees `Started`. Rejected: it misses "Resume game", and the lobby can be gone before the transition. *Alternative:* re-key `TrackingAutoStart` on the active game id from the Game State Service. Rejected: that couples the global auto-start to a session that only exists while a game screen collects it.

### D2. Idempotency stays in the controller

`TrackingController.start` already returns `AlreadyTracking` for the tracked game and `Refused(StoppedBefore)` for a stopped one, and it validates `Started`/`InProgress`/in-play against `/gamestate`. The new caller adds no checks of its own beyond "the screen shows an active game", so a tagged prey re-entering the screen is refused by the controller, as the spec requires.

### D3. Notifications runs as a single replica

`WebPubSubBroadcaster` allocates `seq` from a `ConcurrentDictionary` in the process. The container-app module defaults to `maxReplicas: 2`. Two replicas would emit `seq` 5 and 3 for the same game, and the client treats any non-consecutive `seq` as a gap and resyncs on nearly every message. `notifications/main.bicep` therefore sets `maxReplicas: 1`. *Alternative:* a Redis `INCR` per game. Deferred: correct but adds latency and a dependency for a service that easily fits one replica today.

## Risks / Trade-offs

- [The consent dialog appears on the game screen over the safety warning] → the consent host already queues; the game screen's warning dialog and the consent sheet are tested together on a device (task 4).
- [Calling the starter on every entry costs one `/gamestate` read] → only when tracking does not already run (`AlreadyTracking` short-circuits before the network).
- [A single Notifications replica is a single point of failure] → Container Apps restarts it, and clients resync on reconnect and every 3 minutes. Acceptable at current scale; revisit with D3's alternative if load grows.

## Migration Plan

App release through the normal internal-track pipeline. The Bicep change deploys with `notifications.yml`. No data or protocol migration. Rollback: revert the commit; older app builds keep working (they just need an app restart mid-game, as today).
