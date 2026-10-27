# Design

## Context

See proposal.md for the motivation. The facts that shape the design:

- **Broadcasts are not per sweep.** The leader-elected sweep runs every 30 s, but `Game.SweepLocations` only broadcasts everyone when `NextScheduledBroadcastOn` is due (every `DefaultLocationInterval`, or `FinalLocationInterval` in the final stage). A participant with an active penalty is broadcast on every sweep. All broadcasts of one sweep go out as one `locations-updated` message to the whole game group (Hunter and every Prey), so the payload cannot be personalised per recipient.
- **`GameParticipant.Location` is the public "last known location".** It is set only when the participant is broadcast, and it is served to every caller through `ParticipantDto.Location` (in `GameDto`), `GameParticipantStatusDto.LastKnownLocation` (in `GameStatusDto`) and the Hunter's `GameStateDto.PreyLocations`. Clients resync from `GET /games/{id}/gamestate` every 3 minutes and on seq gaps.
- **`Location` is also used by game logic in two places:** the boundary-penalty fallback in `GameSweepProcessor.ApplyBoundaryPenaltiesAsync` (a player without new readings is checked on `Location`) and the prey's `hunterDistanceMeters` in `GameMappings.ToRoleStateDto`. Tagging and tag candidates already use the latest stored reading.
- Penalty broadcasts (`prey-updated` with `penalized`) carry no coordinates.
- The app never draws the player's own marker from server data (`MarkerStyle.of` returns null for `isMe`; the self marker comes from the device fix). Prey see other prey's markers as well as the Hunter's.
- The radar ping (#50, change `server-games-radar-ping`) reveals a prey's real location and does not consume a pending decoy.

## Goals / Non-Goals

**Goals:**
- The decoy replaces exactly one broadcast of one prey, and nothing anywhere reveals to other players that it was a decoy, including after a resync.
- Every rule that decides the game (tag range, boundary penalties, silence timeouts, end conditions) keeps using real readings.
- At-most-once and never-lost substitution, even when a sweep is retried.

**Non-Goals:**
- More than one decoy per prey, decoys for the Hunter, or decoys that last several broadcasts.
- A visible "decoy" event for anyone but the prey who placed it.
- Decoys from the Wear OS watch (follow-up in `app-wear-os-companion`).

## Decisions

### D1. "Next sweep" means the next sweep that broadcasts this prey

A pending decoy is consumed by the first sweep in which this prey is broadcast: the next regular broadcast, or the next 30 s sweep if the prey has an active penalty. A sweep that does not broadcast the prey leaves the decoy pending. Consuming it on a sweep without a broadcast would waste it silently in most cases (four out of five sweeps with a 150 s interval).

### D2. The decoy becomes the prey's public last known location until their following broadcast

When the decoy is broadcast, the sweep stores the decoy coordinate in `Location` (the public last-broadcast position). It stays there until the prey's following broadcast, which stores and sends the real latest reading again. This is what makes a decoy indistinguishable: `locations-updated`, `GameDto`, `GameStatusDto` and the Hunter's role state all show the same position, and a resync shows the decoy rather than leaking the real position.

*Alternative:* substitute only in the realtime payload and keep `Location` real. Rejected: the Hunter's next resync (at most 3 minutes later) would jump the marker to the real position and expose the decoy.

### D3. Game logic reads real readings, never `Location`

- Boundary-penalty fallback for a player without new readings: use the latest stored reading (`LatestKnownLocation`), and only fall back to `Location` when the player has no readings at all (impossible for a decoy owner, see D5).
- Prey `hunterDistanceMeters` in `ToRoleStateDto`: use the prey's latest stored reading. The Hunter side keeps using the Hunter's `Location` (Hunters can't place decoys, so nothing changes there).
- Tagging, tag candidates, silence timeouts and the head-start penalty already use readings and are unchanged.
- Radar ping (#50) reads `LatestKnownLocation`, never `Location`, and leaves a pending decoy untouched. A ping during the window in which the decoy is the public position therefore shows the real position to the Hunter. That is intended: the radar ping is the Hunter's counter to a decoy.

### D4. Who sees the decoy: everyone except the prey who placed it

The broadcast goes to the whole game group, so the Hunter and every other prey see the decoy. The placing prey's app ignores its own entry for drawing (it already does) and shows its own pending decoy marker from the caller-only `Decoy` state until a `locations-updated` containing its own userId arrives (that entry is the decoy broadcast), then refreshes the state. Personalising the group broadcast for other prey is not possible with Web PubSub group sends and gives no gameplay value.

### D5. Placement rules (maximum distance and when it is allowed)

`Game.PlaceDecoy(userId, coordinate, isInsidePlayfield, now)` enforces, in this order:

| Rule | Status | `code` |
|---|---|---|
| Caller is not a participant | 404 | `game_not_found` (existing) |
| Caller is the Hunter | 403 | `decoy_prey_only` |
| `EnableDecoy` is off | 409 | `decoy_disabled` |
| Game not `InProgress` | 409 | `decoy_not_available` |
| Prey already used their decoy | 409 | `decoy_already_used` |
| Prey is Tagged or Out | 409 | `decoy_not_available` |
| Prey has an active penalty | 409 | `decoy_not_available` |
| Prey has no stored reading yet | 409 | `decoy_no_position` |
| Point outside the playfield polygon | 400 | `decoy_outside_playfield` |
| Point more than 300 m from the prey's latest reading | 400 | `decoy_too_far` |

- **Maximum distance: 300 m** (`Game.DecoyMaxDistanceMeters`, Haversine, measured from the latest stored reading at placement time). It is roughly what a walking prey covers in one 2–5 minute ping interval, so the jump looks plausible, and it stops a prey sending the Hunter to the far side of a large playfield. Distance is not re-checked at broadcast time.
- **Inside the playfield:** checked in the handler with the existing `IPlayfieldBoundaryProvider` + `IBoundaryChecker` and passed to the domain as a boolean (the domain has no polygon). A playfield with fewer than 3 points counts as "inside", like boundary penalties.
- **Not while penalised:** a penalty is meant to expose the prey every 30 s; a decoy during a penalty would undercut it. A penalty that starts after placement does not cancel the pending decoy; it simply makes it go out on the next sweep.
- **Passive prey may place one** (placing it requires an HTTP call, which in practice comes with location reports that make them Active again).
- **Head start and final stage: allowed.** The rules are the same in every phase of `InProgress`. In the final stage the shorter interval means the decoy is visible for less time; that is a natural trade-off the prey makes, not something to forbid.
- Placing a decoy marks it used immediately (`DecoyUsed = true`, `PendingDecoy`, `DecoyPlacedAt`). There is no cancel or move.

### D6. Discard rules

A pending decoy is discarded and stays used when, before it was broadcast:

- the prey is **tagged** (`TagParticipant`),
- the prey goes **Out** (silence timeout or forfeit),
- the prey **becomes the Hunter** (`SetHunter`; the old Hunter who becomes a prey has not used a decoy and may place one),
- the game **completes** (`Complete`).

A prey turning **Passive** keeps the pending decoy: they are still in play and still broadcast on the regular schedule. Each discard is recorded with its reason for the export. A discard is silent (no realtime message); the prey's next status read shows no pending decoy.

### D7. Consumption is part of the sweep's single persisted update

`SweepLocations` substitutes the decoy, writes `Location`, clears `PendingDecoy` and records `DecoyBroadcastAt` on the aggregate in memory; the sweep persists the game once (existing step 5) and publishes events after the save. If the save fails, nothing was published and the decoy is still pending for the retry; if publishing fails after the save, the decoy is consumed but not seen, the same at-most-once delivery every broadcast already has. A retried sweep never sends it twice because the pending decoy was already cleared in the saved state.

### D8. Persistence

- `Games.EnableDecoy` (bool, default false) on the owned `Configuration`.
- `GameParticipants`: `DecoyUsed` (bool, default false), owned `PendingDecoy` (`PendingDecoyLatitude`/`PendingDecoyLongitude`, nullable), `DecoyPlacedAt` (nullable), and a jsonb `Decoy` record for the export (`PlacedAt`, location, `BroadcastAt` or `DiscardedAt` + reason). One EF migration; existing rows default to "unused".

### D9. Contract

- `GameConfigurationDto` gets a trailing `bool EnableDecoy = false`; `UpdateGameSettingsRequest` and `CreateGameRequest` get the same optional field. `GameConfiguration.Create` gets `enableDecoy = false`. An older app that saves settings sends no field and turns the decoy off; that is acceptable because such an owner could not have turned it on.
- `POST /games/{id}/decoy` with `PlaceDecoyRequest(double Latitude, double Longitude)` returns `200` with `DecoyStatusDto`.
- `DecoyStatusDto(bool Available, GpsCoordinateDto? Pending, DateTimeOffset? PlacedAt)`. `GameStatusDto` gets a trailing `DecoyStatusDto? Decoy = null`: set only for the calling prey when `EnableDecoy` is on; `null` for the Hunter, non-participants and when disabled. The Hunter's view (`GameDto`, role state, other participants' statuses) never contains decoy fields.
- Export: `ParticipantExportDto` gets a trailing `DecoyExportDto? Decoy = null` (`PlacedAt`, `Location`, `RealLocationAtPlacement`, `BroadcastAt`, `DiscardedAt`, `DiscardReason`).
- No new realtime message type and no change to `locations-updated`.

### D10. Observability

`PlaceDecoyCommandHandler` starts activity `PlaceDecoy` with outcome tag (`placed` or the rejection `code`). Counters `games.decoys.placed` and `games.decoys.broadcast` (the latter recorded by the sweep), plus `games.decoys.discarded` tagged with the reason (`tagged`, `out`, `became_hunter`, `game_ended`). No user or game ids as metric tags.

### D11. Android app

- `GameSettings` gets `decoyEnabled: Boolean = false`; `GameSettingsForm` gets a switch row (owner-editable, read-only summary chip for others); `toSettings()` / `with()` map `enableDecoy`.
- Prey HUD: a decoy button visible when `status.decoy?.available == true`. Tapping enters a pick mode on `GameMap` (tap or long-press to set a point, a 300 m circle around the device fix and the playfield outline as guides), then a confirm sheet stating it is one-time. On success the HUD shows a distinct "decoy" marker at the pending point until the own `locations-updated` entry arrives or a resync shows no pending decoy. Rejection codes map to localized messages.
- `GamesApi.placeDecoy`, `GameRepository` sealed outcome, `GameStateDto` additions with defaults (older servers omit them).
- Hunter HUD unchanged.

## Risks / Trade-offs

- [A prey on an old app cannot place a decoy, while others can] → The feature is opt-in per game and the toggle is new; the update policy can mark the release `update:required` if needed.
- [Hunter infers a decoy from an implausible jump] → Capped at 300 m and inside the playfield; jumps of that size already happen between real pings.
- [Placing a decoy reveals intent through timing] → No realtime event at placement; only the broadcast changes.
- [Boundary fallback change alters penalty behaviour for players without new readings] → It moves from "last broadcast position" to "latest stored reading", which is at least as fresh; covered by tests. Record in the game rules.
- [The prey's own `GameDto` shows the decoy as their location] → The app never draws itself from server data; documented in `docs/api/endpoints.md`.

## Migration Plan

1. Ship the server (migration adds columns with defaults; the endpoint and DTO fields are additive). Older apps keep working and never see the setting.
2. Ship the app with the toggle and HUD button. Until then the setting stays off because no client can turn it on.
3. Rollback: the server can be rolled back after the migration since the new columns are ignored by the old model; games with `EnableDecoy` simply behave as without decoys.
