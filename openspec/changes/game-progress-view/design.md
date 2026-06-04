## Context

The pieces around the in-game experience already exist:

- **Server**: `GET /games/{id}/state` returns role-specific state (`hunterDistanceMeters` for preys, `preyLocations` for hunters). The `Game` domain model already exposes `ScheduledEndAt` (start + duration).
- **App engine** (app-background-service): `GameEngineService` runs a location-push loop (server-controlled interval, penalties) and a 15-second state-sync loop, writing into the observable `GameStateContext` singleton.
- **App navigation** (game-start-view): after starting a game, the lobby view navigates to a placeholder `GameProgressPage` carrying the game id and the local player's role.
- **Map rendering**: the playfield pages already render OpenStreetMap tiles + a playfield polygon with Mapsui (`WritableLayer`, `GeometryFeature`, `SphericalMercator`), so all map plumbing is proven.

This change turns the placeholder into the real Game Progress view: a map (playfield overlay, own position, prey positions for the hunter) above a role-specific HUD with countdowns and distances.

## Goals / Non-Goals

**Goals:**
- Replace the placeholder `GameProgressPage` with the real map + HUD view.
- Show the playfield polygon semi-transparently, a green dot for the local player, and (hunter only) red dots per prey.
- Role-specific HUD: remaining game time, time until next location send, hunter distance (prey, in red, with "measured X ago") / nearest-prey distance (hunter); dashes when unknown.
- Make the countdown server-authoritative by adding `gameEndsAt` to the state response.
- Expose `CurrentLocation` and `NextLocationPushDueAt` from the engine so the view never talks to GPS or HTTP itself.
- Start/stop the game engine from the page lifecycle (completes task 9.x wiring of app-background-service).

**Non-Goals:**
- Catch/tag mechanics, scoring, or any end-of-game results screen (only a "game ended" signal + navigation away).
- Server push (WebSocket/SignalR) — state stays polled.
- Map interaction features beyond pan/zoom defaults (no tap actions on dots).
- Final-stage visual effects or penalty banners beyond what `GameStateContext` already exposes.
- Persisting map state across app restarts.

## Decisions

### 1. Server-authoritative game end time via `gameEndsAt` in the state response

**Decision**: Extend `GameStateDto` with `GameEndsAt` (nullable `DateTimeOffset`), populated from `Game.ScheduledEndAt` in `GetGameStateQueryHandler`. The engine copies it to `GameStateContext.GameEndsAt`; the page computes "remaining" locally every second.

**Rationale**: The domain already computes `ScheduledEndAt`; piggy-backing on the existing 15-second sync avoids an extra `GET /games/{id}` fetch and keeps the countdown correct even if the page opens mid-game (e.g. after app restart). Device clock skew affects the display by at most the skew amount — acceptable for a casual game.

**Alternative**: fetch `GameDto` once on page load and compute end = `StartedAt + GameDuration` — rejected: an extra request, and it goes stale if the server ever changes the schedule.

### 2. HUD countdowns tick locally from context timestamps

**Decision**: The page runs a single 1-second UI timer (dispatcher timer) that recomputes both countdowns from `GameStateContext.GameEndsAt` and `GameStateContext.NextLocationPushDueAt`, clamping at 00:00.

**Rationale**: `PropertyChanged` from the context fires only when the engine writes (every push/sync); a smooth mm:ss countdown needs a local ticker. One timer drives all time-derived labels (including the "measured X s ago" caption from `LastStateSyncAt`).

**Alternative**: have the engine raise per-second updates — rejected: turns a UI concern into engine work and spams the context.

### 3. Engine exposes `NextLocationPushDueAt` and `CurrentLocation`

**Decision**: After every push cycle, `GameEngineService` sets `GameStateContext.NextLocationPushDueAt = now + effective interval` (penalty-aware) and `GameStateContext.CurrentLocation` to the GPS fix used (also set when a fix is acquired but the push fails, so the green dot stays honest). Both are null until first acquisition.

**Rationale**: The view must never call `Geolocation` or compute intervals itself — the engine already owns both. The countdown then exactly matches what the engine will actually do, including penalty overrides.

**Alternative**: expose `CurrentPushIntervalSeconds` and let the page do the math from `LastLocationPushedAt` — rejected: duplicates penalty/interval logic in the UI and drifts from the real timer.

### 4. Nearest-prey distance computed on-device

**Decision**: For the hunter, the page computes haversine distance from `CurrentLocation` to each `PreyLocations` entry and shows the minimum; a dash when either side is missing. A small static `GeoMath.DistanceMeters` helper in the app holds the formula.

**Rationale**: Both inputs are already on the device and refresh on different cadences (GPS each push, prey each sync); recomputing locally keeps the HUD as fresh as possible. The server's `hunterDistanceMeters` (for prey) stays server-computed because a prey never receives hunter coordinates — only the distance.

**Alternative**: add `nearestPreyDistanceMeters` to the state response — rejected: it would lag the hunter's own movement by up to 15 s for no gain.

### 5. Map composition reuses the proven Mapsui patterns

**Decision**: `GameProgressPage` builds its map exactly like the playfield pages: OSM tile layer + one `WritableLayer` for the playfield polygon (semi-transparent fill, visible outline) + one `WritableLayer` for the dots (green = self, red = preys). Dot layers are rebuilt when `CurrentLocation`/`PreyLocations` change; the map view is fitted initially to the playfield bounds.

**Rationale**: Same library, same projection helpers (`SphericalMercator`), same layer approach already shipped in `PlayfieldDetailsPage` — minimal new surface. Per the project guideline on reusable controls, the polygon-building code that now exists in two pages should be extracted into a shared helper (`Controls/` or a static map-builder) as part of this change rather than copied a third time.

**Alternative**: a new dedicated map control library — rejected, Mapsui is already a dependency and proven here.

### 6. Playfield geometry comes from the local cache

**Decision**: The page resolves the playfield polygon from `PlayfieldCacheService` by the game's `PlayfieldId` (the id travels with navigation from the lobby view). When the playfield is not in the cache, the map shows tiles + dots without an overlay and the game remains fully playable.

**Rationale**: The cache is the app's source of truth for playfields, and the creator (current flow) always has the selected playfield cached. Fetching arbitrary playfields by id needs a server endpoint that doesn't exist yet.

**Alternative**: add `GET /playfields/{id}` server support — deferred until joining foreign games (join-by-code change) makes it necessary.

### 7. Page lifecycle drives the engine

**Decision**: `OnAppearing` calls `IGameEngineService.StartAsync(gameId, role)` (idempotent if already running); a "leave game" action and `GameStateContext.GameEnded == true` both lead to `StopAsync()` + navigation back to the main menu. The page subscribes to `GameStateContext.PropertyChanged` and reacts to `GameEnded` with a localized alert before navigating away.

**Rationale**: The engine start must be tied to entering the in-game experience, not app start. Idempotent `StartAsync` makes re-entry (back navigation, resume) safe.

## Risks / Trade-offs

- **Device clock skew distorts countdowns** → both countdowns derive from server/engine timestamps compared against local now; skew shifts the display but not engine behaviour. Acceptable; no mitigation beyond using `DateTimeOffset.UtcNow` consistently.
- **Prey dots lag up to 15 s + sender interval** → inherent to polling; the HUD's "measured X ago" caption (prey) and dot freshness (hunter) make staleness visible instead of hiding it.
- **Playfield missing from cache for joined games** → degrade gracefully (no overlay); revisit when join-by-code lands.
- **Mapsui layer churn every sync** → dot layers contain at most a handful of features; clearing and re-adding per update is cheap. Polygon layer is built once.
- **Two pages already duplicate polygon-drawing code; a third copy compounds it** → extract the shared helper in this change (Decision 5).
- **`GameProgressPage` placeholder from game-start-view may still be in flight** → coordinate: this change owns the page's real content; if the placeholder hasn't merged yet, implement the page directly in this change and let game-start-view wire navigation to it.

## Open Questions

- Should the hunter's red prey dots disappear when stale (e.g. older than 2× the prey's reporting interval), or persist at the last known spot? (Current design: persist — matches "last known location" semantics.)
- Should leaving the page (back navigation) stop the engine or keep it running in the background? (Current design: explicit leave/game-end stops it; plain back navigation keeps it running so the player can peek at other screens — matches the engine's app-wide lifecycle design.)
