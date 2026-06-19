## Why

Today the hunter can tag any prey that is `Active` or `Passive`, regardless of how far away that prey is — the client builds the tag list purely from the game's participant roster. This breaks the core fiction of the game: tagging should require the hunter to physically catch up to a prey. We need the server (the authority on positions) to decide who is actually within catching range so tagging reflects real-world proximity and cannot be spoofed by the client.

## What Changes

- Add a new server endpoint that returns the preys the hunter is currently allowed to tag, based on GPS proximity.
  - The server uses the **most recent emitted location** from the hunter's location history (the latest reading in the `locations` array), not a stale "last known" field.
  - For each prey, the server uses that prey's **most recent emitted location** from its own location history.
  - Distance is computed with the existing Haversine helper. Preys within **50 meters** of the hunter and in state `Active` or `Passive` are returned as taggable candidates; preys further away are excluded.
- **BREAKING (client behavior)**: The hunter's "Tag Player" drawer no longer lists every active/passive prey computed locally. Instead, when the hunter opens the drawer the client calls the new endpoint and lists only the preys returned (those in range). Preys out of range are not tagged.
- The existing tag endpoint (`POST .../tag`) is unchanged in contract, but a proximity guard is added server-side so a tag for an out-of-range prey is rejected — the candidate list and the tag action must agree.

## Capabilities

### New Capabilities
- `tag-candidates-endpoint`: Server endpoint that returns the in-range, taggable preys for the hunter based on the most recent emitted GPS locations of the hunter and each prey, within a 50-meter radius.

### Modified Capabilities
- `tag-player-action`: The hunter HUD tag flow changes — the list of taggable preys is sourced from the new proximity endpoint instead of being computed client-side from the full roster, and the tag action is guarded by the same proximity rule server-side.

## Impact

- **Server (`src/Games`)**: New feature slice (command/query handler, endpoint, registration, OTel). New tag-range domain constant (50 m). Proximity validation added to the existing `TagParticipant` domain logic. New DTOs in `Games.Abstractions`. Reuses `GpsCoordinate.DistanceInMetersTo` and per-participant `LatestKnownLocation` (most recent reading from the `_locations` history). New unit tests.
- **Client (`src/ThePrey`)**: `games.service.ts` gets a new method to fetch tag candidates. `game-hunter.page.ts` opens the tag drawer by calling the endpoint instead of using the locally computed `taggablePrey` signal; empty/loading/error states for the drawer.
- **No DB schema change** — location history (`Locations` jsonb) and participant state already exist.
