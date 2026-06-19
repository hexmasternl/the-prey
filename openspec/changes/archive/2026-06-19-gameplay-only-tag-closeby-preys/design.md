## Context

The hunter tags preys via `POST /games/{gameId}/participants/{participantId}/tag`. The client (`game-hunter.page.ts`) decides which preys are taggable purely from the participant roster (`recomputeTaggable()` filters by state `Active`/`Passive`). Proximity is never considered, so a hunter can tag a prey on the other side of the playfield.

The server already has everything needed to judge proximity:
- Each `GameParticipant` keeps a `_locations` history of `LocationReading(Id, Coordinate, RecordedAt, Checked)` and exposes `LatestKnownLocation` — the most recent reading by `RecordedAt`.
- `GpsCoordinate.DistanceInMetersTo(other)` is a Haversine implementation already used by the head-start penalty logic.
- The location history is persisted as a `Locations` jsonb column; no schema change is needed.

This change moves the "who can be tagged" decision to the server and gates it on a 50-meter radius.

## Goals / Non-Goals

**Goals:**
- Add a server endpoint that returns the preys the hunter may tag right now, computed from the most recent emitted GPS location of the hunter and of each prey, within 50 m.
- Make the hunter's tag drawer populate from that endpoint.
- Guard the existing tag action server-side with the same proximity rule so the candidate list and the tag action cannot disagree.

**Non-Goals:**
- No change to how locations are emitted, stored, or swept.
- No change to the tag state machine (`Active`/`Passive` → `Tagged`), authorization (hunter-only), or game-end logic.
- No real-time push of candidate changes — the client fetches on demand when the drawer opens.
- No change to the playfield boundary checking.

## Decisions

### Decision: New read endpoint `GET /games/{gameId}/tag-candidates`
A dedicated query endpoint returns the in-range preys. It reuses the auth + hunter-only checks of the tag flow.

- **Shape**: returns `{ rangeMeters, candidates: [{ userId, callsign, state, distanceMeters }] }`. Including `distanceMeters` lets the client sort/label "closest first" without recomputing.
- **Why a query, not reusing `/status`**: `/status` returns the full roster with raw coordinates and no proximity judgment. Proximity is a server decision and the client should not need prey coordinates to make it. A focused endpoint keeps the rule on the server and avoids leaking every prey's exact position to the hunter.
- **Alternative considered**: add a `canTag`/`distanceMeters` field to each participant in `/status`. Rejected — it couples an unrelated, frequently-polled DTO to the tag feature and still streams all coordinates to the client.

### Decision: "Most recent emitted location" = `LatestKnownLocation`
Both hunter and prey positions are taken from the latest reading in their `_locations` history (`LatestKnownLocation`), explicitly **not** any cached `Location`/`LastKnownLocation` snapshot field, matching the requirement to use the most recent entry in the locations/positions array.

- A participant with no location history is treated as not-in-range (hunter with no location ⇒ empty candidate list; prey with no location ⇒ excluded).

### Decision: 50 m threshold as a domain constant
Add `TagRangeMeters = 50` to the `Game` domain constants alongside the existing thresholds. Both the candidate query and the `TagParticipant` guard read this single constant.

- Candidacy rule: prey state is `Active` or `Passive` **AND** `hunter.LatestKnownLocation.DistanceInMetersTo(prey.LatestKnownLocation) <= TagRangeMeters`.

### Decision: Proximity guard inside `TagParticipant`
`Game.TagParticipant` gains a proximity check (after the existing state/auth/head-start checks) that throws when the target is out of range, surfaced as HTTP 409 by the endpoint. This keeps the domain authoritative and prevents a stale client from tagging an out-of-range prey.

- **Why 409**: consistent with the existing "invalid operation" mappings on the tag endpoint (game not in progress, prey not taggable). Out-of-range is a transient conflict, not a missing resource or auth failure.

### Decision: Client fetches on drawer open
`openTagModal()` becomes async: it calls `gamesService.getTagCandidates(gameId)`, shows a loading state, then lists the returned candidates. The locally computed `taggablePrey` signal is replaced by a `tagCandidates` signal populated from the response. Empty result ⇒ "No preys in range" message; error ⇒ retryable message.

## Risks / Trade-offs

- **Stale positions** → A prey may have moved since its last emitted reading (heartbeat is ~10 s), so the radius reflects positions up to one interval old. Accepted — this is inherent to a location-emitted game and matches existing sweep behavior.
- **Candidate list vs. tag race** → A prey can leave range between the candidate fetch and the tag tap. Mitigated by the server-side proximity guard on `TagParticipant` (returns 409); the client surfaces the failure and the hunter can reopen the drawer.
- **No location yet for hunter** → Empty candidate list even though preys exist. Acceptable and correct; the drawer shows "No preys in range."
- **Slightly more coupling between query and domain constant** → Both the query and the guard depend on `TagRangeMeters`; centralizing it as one constant avoids drift.
