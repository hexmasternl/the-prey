# Proposal

## Why

The server can only enforce the playfield boundary (and the hunter's head-start rule) with positions the phone actually sends. Today nothing sends them while the app is closed or the screen is off, so a player can stop the app, walk out of the playfield and come back without the server ever seeing it. A foreground service that reports the precise position every 5 seconds for the whole game closes that gap: leaving the playfield shows up in the location history and gets penalized like any other boundary violation.

## What Changes

- New Android foreground service (type `location`) that, while the signed-in player takes part in an active game, posts a high-accuracy GPS fix to `POST /games/{id}/locations` every 5 seconds, including when the app is in the background, swiped away, or the screen is off.
- The service is started **only** for an active game (`Started` or `InProgress`) in which the player is still in play. It is never started from the menus, the lobby, or for a game that has ended.
- On start the service reads the game's end time (`endsAt`) and arms a hard deadline. It stops on whichever comes first: the deadline passes, the server reports the game `Completed` (realtime `game-ended` or a confirming `GET /games/{id}/gamestate`), the server says the player no longer has to report (tagged or out), the game disappears or the player is no longer a member, or the player signs out. A stopped service never restarts itself for that game.
- Resume: when the app starts and a player is signed in, it asks the server for the player's active game (`GET /games/active`) and starts the service if there is one. After a reboot or app update the app cannot start a location service by itself (Android 14+ blocks this), so a boot receiver posts a "game in progress, tap to resume tracking" notification for a still-running game. Tapping it opens the app, which starts the service again.
- Persistent, non-dismissable notification while tracking runs: the game, a countdown to the end, and a warning when location is off or the permission was revoked.
- Readings that cannot be sent (no network) are kept in a small bounded buffer and sent in order once the connection is back, so dropping mobile data does not hide where the player went.
- Server: the location acknowledgement gets an additive `keepReporting` flag (false once the caller is `Tagged` or `Out`), and the advertised reporting interval constant becomes 5 seconds to match the device cadence.
- Manifest: `FOREGROUND_SERVICE`, `FOREGROUND_SERVICE_LOCATION`, `RECEIVE_BOOT_COMPLETED`, `WAKE_LOCK`, the `<service android:foregroundServiceType="location">` declaration and the boot receiver. `ACCESS_BACKGROUND_LOCATION` is not added here: `app-play-now-button-behavior` declares it and makes "Allow all the time" a precondition for playing (checked by the main menu, granted only on the system settings page), so this change can rely on it but does not need it to start the service.

## Capabilities

### New Capabilities
- `in-game-location-tracking`: The Android in-game location reporting service: when it may run, its 5-second reporting cadence, how it learns the game's end and stops, resume after process death, app restart and reboot, offline buffering, and the tracking notification.

### Modified Capabilities
- `games`: Two added requirements. The location acknowledgement tells the caller whether it must keep reporting (`keepReporting`, false once the participant is `Tagged` or `Out`), and the advertised regular reporting interval is 5 seconds. The existing "Recording player locations" requirement is unchanged.

## Impact

- **Android app (`app/src`)**: new `tracking/` package (foreground service, tracking controller, session store, boot receiver, games API client for `locations`/`active`/`gamestate`), `AppContainer` wiring, a `FusedLocationSource` high-accuracy variant, the manifest entries above, and new notification strings in English and Dutch (`the-prey-multilingual`).
- **Depends on** `app-location-foreground-service-warning`: the service only runs with fine location permission (and notification permission on Android 13+) granted through that consent flow. If permission is missing when a game starts, the consent gate from that change runs first.
- **Server (Games module)**: `RecordLocationResponse` gets `KeepReporting` (additive, no break for existing clients), `RecordPlayerLocationCommandHandler` fills it, `Game.LocationReportingIntervalSeconds` goes from 10 to 5, and the matching unit tests are updated.
- **Docs**: `docs/api/endpoints.md` Location section corrected (route is `/locations`, hunter and prey both report, fields `latitude`/`longitude`/`recordedAt`/`accuracy`, `keepReporting`).
- **Play Console**: the foreground service `location` type declaration (with a video of the in-game use) and the Data safety form must match this behavior.
- **Battery**: high-accuracy GPS plus a 5-second network post runs for the full game length, typically 30 to 120 minutes. This is accepted as the price of tamper resistance.
