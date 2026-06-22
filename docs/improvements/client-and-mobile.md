# Improvements — Client & Mobile

Opportunities in the Ionic/Angular app (`src/ThePrey`). See [architecture/client.md](../architecture/client.md) for the current design.

## 1. Offline location queue — Impact: High · Effort: M

GPS posts go straight to `POST /games/{id}/locations`. A connectivity gap (tunnel, dead zone) means missed reports, and the server can time a prey out to `Out` through no fault of theirs.

- Buffer readings in IndexedDB when a post fails; flush in order on reconnect with their original timestamps.
- Surface the existing `reportingDegraded` signal more prominently and pair it with the buffered count.
- Server-side: tolerate a short burst of backfilled timestamps rather than penalizing.

## 2. iOS support — Impact: High · Effort: L

Android is the shipping target (foreground-service background geolocation). iOS background location is a different model (significant-location-change / region monitoring, stricter permissions).

- Validate `@capacitor-community/background-geolocation` on iOS or add a platform-specific strategy.
- Add the App Store equivalent of the Android release pipeline.

## 3. Battery & motion-aware reporting — Impact: Med · Effort: M

Cadence is server-driven, which is good, but the client always reports on schedule even when stationary.

- Throttle/suppress posts when motion sensors say the player hasn't moved (the server can treat "no change" as a keep-alive).
- Use geofencing on the playfield boundary to wake/raise cadence near the edge instead of polling continuously.

## 4. Accessibility audit of the tactical theme — Impact: Med · Effort: S–M

The phosphor-green-on-near-black aesthetic (`--tp-signal` on `--tp-bg-void`) is striking but risks low contrast, and `PT Mono` body text at small sizes can be hard to read in sunlight (an outdoor game!).

- Run a WCAG contrast pass; provide a high-contrast / high-visibility outdoor mode.
- Respect OS font scaling; verify hit targets meet 44px.
- Ensure status/penalty information isn't conveyed by color alone (add icons/text).

## 5. Map robustness for the field — Impact: Med · Effort: M

Leaflet with online raster tiles degrades in poor connectivity — exactly the outdoor condition the game runs in.

- Cache/prefetch tiles for the playfield bounding box when a game starts.
- Consider vector tiles or an offline tile pack for known playfields.

## 6. Richer spectator experience — Impact: Med · Effort: S

Tagged/finished players stay connected as spectators until game end. Make that time engaging rather than a dead screen.

- Spectator view of the live hunt (respecting the prey-location-only-to-hunter rule — spectators can see an anonymized/aggregate view or the full board post-tag).
- A "time remaining + who's left" HUD.

## 7. Join / deep-link flow polish — Impact: Low–Med · Effort: S

`/games/join` self-restores the session and bypasses the auth guard. Harden the edge cases.

- Handle expired/cancelled games and already-started games with clear messaging instead of a generic error.
- Cold-start deep link → login → resume the intended join target reliably.

## 8. In-app boundary warning before penalty — Impact: Med · Effort: S

Players currently learn they're out of bounds via a penalty event. Warn them first.

- Client-side proximity-to-edge warning (haptic + visual) before the server applies a penalty, using the playfield polygon already on the map.

## 9. Client telemetry & crash reporting — Impact: Med · Effort: S

The backend is well-instrumented; the client is comparatively dark.

- Add lightweight client telemetry (session, GPS permission state, reconnect counts, post-failure rate) and crash reporting, feeding the same observability story.
