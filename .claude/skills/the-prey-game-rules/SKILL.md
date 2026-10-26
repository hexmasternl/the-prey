---
name: the-prey-game-rules
description: The authoritative, condensed game rules and domain glossary of The Prey — roles, game lifecycle (Lobby, Ready, Started, InProgress, Completed), configuration parameters with their real defaults and bounds, head start, location pings and sweep cadence, tagging within 50 m, penalties, going silent (Passive/Out), win conditions and playfield constraints. Use whenever you implement, change, test, document or explain game behaviour on the server, in the Android app or on the website; when writing game-related copy, OpenSpec proposals or unit tests; when you need a constant (tag range, penalty length, timeouts, lobby size); and when docs, app and server seem to disagree.
metadata:
  author: the-prey
  version: "1.0"
---

# The Prey — Game Rules

**The server code is the source of truth.** The rules below are taken from the Games domain in `server/src/Games/HexMaster.ThePrey.Games/` (mainly `DomainModels/Game.cs`, `GameConfiguration.cs`, `GameParticipant.cs` and `GameEngine/GameSweepProcessor.cs`). Where `docs/game-design/*.md` or an `openspec/specs/*` file says something else, the code wins — see *Known drift* below.

## Roles

| Role | Who | Rules |
|---|---|---|
| **Game Creator** (owner) | Player who created the game (`Game.OwnerUserId`) | Joins the lobby automatically as first participant. Only one who can change settings, designate the Hunter in the lobby, remove lobby players and start the game. Never needs to "ready up". Plays as Hunter or Prey. Leaving the lobby cancels the game. |
| **Hunter** | Exactly **one** per game (`Game.HunterUserId`) | Waits out the head start without moving; tags Prey within 50 m. During a running game the current Hunter can hand the role to a Prey (`POST /games/{id}/hunter`). |
| **Prey** | Every other participant (derived: `Game.Preys` = everyone except the Hunter) | Hides and keeps reporting location. Can be Tagged or go Out. |

There is no role field on a participant: the role is derived from `HunterUserId`. Never add "multiple hunters" logic — the domain does not support it.

## Lifecycle

```
Lobby ⇄ Ready ──(owner starts: Arm)──▶ Started ──(next sweep: BeginPlay)──▶ InProgress ──▶ Completed
  └──────────(owner leaves: CancelByOwner, outcome Cancelled)──────────────────────────────▶ Completed
```

| Status | Meaning | Allowed actions |
|---|---|---|
| `Lobby` | Gathering players | Join (4-digit code), leave, designate Hunter, ready up, change settings, remove player |
| `Ready` | Auto-set when ≥ 2 players, a designated Hunter who is a participant, and every non-owner is ready. Reverts to `Lobby` automatically when that stops being true | Same as Lobby, plus owner may start |
| `Started` | Owner committed (roles fixed); no clock yet. Clients navigate to gameplay | Wait for the sweep |
| `InProgress` | Clock running. `StartedAt` = sweep time − 3 s, `EndsAt = StartedAt + GameDuration` | Report location, tag, hand over Hunter role |
| `Completed` | Ended; `CompletedAt` and `Outcome` set | Read only. Record deleted after `CleanUpAfter` |

- A settings change resets every non-owner's ready flag (so a `Ready` game falls back to `Lobby`).
- Only `Lobby`/`Ready` players can leave. A non-owner leaving is removed (Hunter designation cleared if it was them); the owner leaving cancels the game (`Outcome = Cancelled`).
- **Nobody can end a running game.** It ends only on its own terms (see *End conditions*).
- Games are hard-deleted 48 h after creation (`CleanupWindowHours`, `GameCleanupService` runs hourly).

## Configuration

Durations are **minutes**, intervals are **seconds** on the wire (`GameConfigurationDto`, `CreateGameRequest`, `UpdateGameSettingsRequest`). The app shows and picks every value in minutes and converts ping intervals ×60.

| Parameter | Server rule (`GameConfiguration.Create`) | App options — **default** (`ui/games/create/GameSettings.kt`) |
|---|---|---|
| `GameDuration` | > 0 | 30, 60, 90 — **30** |
| `HunterDelayTime` (head start) | ≥ 0 and < `GameDuration` | 3, 5, 10 — **5** |
| `FinalStageDuration` | > 0 and < `GameDuration` | 3, 5, 10 — **10** |
| `DefaultLocationInterval` | > 0 | 120, 180, 300 s (2, 3, 5 min) — **180** |
| `FinalLocationInterval` | > 0 and ≤ `DefaultLocationInterval` | 60, 120, 180 s (1, 2, 3 min) — **60** |
| `EnablePreyBoundaryPenalties` | default `false` | always sent `false` (not offered yet) |
| `EnableHunterBoundaryPenalty` | default `false` | always sent `false` (not offered yet) |

The server has no defaults and no option lists — only the invariants above. Invalid combinations throw and the endpoint returns a validation problem. Changing a default or option list is an app change (and a website/docs change, see below).

## Fixed constants (`Game.cs`)

| Constant | Value | Meaning |
|---|---|---|
| `MinimumPlayersToStart` | 2 | One Hunter + at least one Prey |
| `MaxLobbySize` | 16 | Including the creator |
| `GameCodeLength` | 4 | Join code is exactly 4 decimal digits |
| `TagRangeMeters` | 50 | Max Hunter–Prey distance for a tag (Haversine) |
| `HunterDelayMovementThresholdMeters` | 25 | Hunter may drift this far from their first head-start fix |
| `HunterDelayPenaltyMinutes` | 10 | Head-start penalty runs until `HunterMayMoveAt + 10 min` |
| `PenaltyDurationMinutes` | 5 | Boundary penalty length |
| `PenaltyReportingIntervalSeconds` | 10 | Interval reported to a penalised device |
| `LocationReportingIntervalSeconds` | 5 | Every device posts its GPS fix every 5 s, always |
| `SweepIntervalSeconds` | 30 | Leader-elected sweep cadence (`GameTickService`) |
| `CleanupWindowHours` | 48 | Record lifetime |
| Passive / Out timeouts | 5 / 7 min | In `GameParticipant.TryTransitionByTimeout` |

## Locations, pings and the sweep

- The phone posts its position every **5 s** (`POST /games/{id}/locations`) for the whole game, Hunter and Prey alike, also during the head start. The server stores every reading.
- **Broadcasting is server-side only.** Each 30 s sweep (`GameSweepProcessor`): promote `Started` → `InProgress`, apply Passive/Out timeouts, then broadcast. A *regular* broadcast happens when `NextScheduledBroadcastOn` is due: **every** participant's latest known position goes out in one `locations-updated` message, and the schedule advances by `DefaultLocationInterval`, or `FinalLocationInterval` once inside the final stage. The first sweep broadcasts immediately (schedule is seeded to `StartedAt`).
- **Final stage** = the last `FinalStageDuration` minutes before `StartedAt + GameDuration`.
- A participant with an **active penalty** is broadcast on *every* sweep (so effectively every 30 s), without moving the regular schedule.
- Hunter and Prey positions are both broadcast to the whole game group; the Hunter is visible to the Prey. Treat every marker as a last known location.

## Head start (hunter delay)

- `HunterMayMoveAt = StartedAt + HunterDelayTime`. Before that the Hunter cannot tag (409).
- The Hunter's first head-start reading becomes `DelayAnchorLocation`. If any head-start reading is > 25 m from it, the Hunter gets **one** penalty ending at `HunterMayMoveAt + 10 min` (reason `moved-during-delay`). Applied at most once per game, assessed every sweep, **not** gated by any configuration flag.

## Tagging

`POST /games/{id}/participants/{participantId}/tag` succeeds only when all hold:

1. Caller is the current Hunter (else **403**).
2. Game is `InProgress` and the head start has elapsed (else **409**).
3. Target is a Prey of this game (else **404**) in state `Active` or `Passive` (else 409).
4. The Haversine distance between the Hunter's **latest stored reading** and the Prey's latest stored reading is ≤ 50 m (else 409). Missing positions block the tag. (Latest *reading*, not the last broadcast position.)

`GET /games/{id}/tag-candidates` returns exactly the Prey that would pass rule 3–4, with distances and the 50 m range. A tagged Prey becomes `Tagged` (irreversible), stops reporting (`keepReporting = false`) and stays connected as a spectator until `game-ended`.

## Player states (`PlayerState`)

| State | Entered when | Notes |
|---|---|---|
| `Active` | Any location report (unless Tagged/Out) | Initial state |
| `Passive` | Prey silent ≥ 5 min | Back to Active on the next report. Still taggable, still counts as surviving |
| `Out` | Prey silent ≥ 7 min (or `Forfeit` in the domain) | Irreversible. Counts as caught |
| `Tagged` | Hunter tagged them | Irreversible. Counts as caught |

Timeouts apply to Prey only, never to the Hunter. They run in the sweep (step 1). `Game.Forfeit` exists in the domain but no endpoint calls it.

## Penalties

| Penalty | Trigger | Length | Flag |
|---|---|---|---|
| Moved during head start | Hunter > 25 m from anchor before `HunterMayMoveAt` | until `HunterMayMoveAt` + 10 min | none — always on |
| Left the playfield | A reading consumed by the sweep is outside the playfield polygon (ray casting); a player without new readings is checked on their last broadcast position | 5 min from detection | `EnablePreyBoundaryPenalties` (Prey) / `EnableHunterBoundaryPenalty` (Hunter) |

Boundary penalties never stack while one is active and are never applied to Tagged/Out players. A playfield with < 3 points never penalises. While penalised, the device is told a 10 s interval and the player is broadcast every sweep. Penalties are announced as `prey-updated` with `penalized` and the reason.

## End conditions and outcome

The sweep checks after the timeout step; `TagPlayer` short-circuits the last tag for instant feedback.

| Condition | Outcome |
|---|---|
| Every Prey is `Tagged` or `Out` | `HuntersWin` (completion reason `all-preys-caught`) |
| `now ≥ StartedAt + GameDuration` with at least one Prey still Active/Passive | `PreysWin` (`duration-elapsed`) |
| Owner left before start | `Cancelled` |
| No Prey at all at completion | `Undecided` |

## Playfields (`server/src/PlayFields/.../DomainModels/PlayField.cs`)

- A closed polygon of **at least 3 points**; the last point connects to the first. Centre = average of the points.
- Public listing only when `IsPublic` **and** the name matches `CC, City, Fieldname` (e.g. `NL, Amsterdam, Vondelpark`); otherwise it is silently saved private.
- Edits use last-write-wins on `LastUpdatedOn` (stale write → 409).

## Glossary

Use the EN/NL glossary in **the-prey-multilingual** for every player-facing term (Hunter/Jager, Prey/Prooi, Game Creator/Spelmaker, Playfield/Speelveld, Head start/Voorsprong, Penalty/Straf, tag/taggen, …). Domain terms used in code and docs:

| Code term | Player-facing EN | Player-facing NL (as on the website) |
|---|---|---|
| Owner | Game Creator | Spelmaker |
| `HunterDelayTime` | Hunter delay / head start | Vertraging jager / voorsprong |
| `FinalStageDuration` | Final stage | Eindfase |
| `DefaultLocationInterval` | Location ping | Locatie-ping |
| `FinalLocationInterval` | Ping in final stage | Ping in eindfase |
| `Passive` | Passive | Passief |
| `Out` | Out | Eruit |
| Tagged spectator | Spectator | Toeschouwer |
| Going silent | Going silent | Stil vallen |

If you introduce a new player-facing term, add it to the glossary in the-prey-multilingual, not here.

## Known drift (code wins)

- `docs/game-design/*.md` is outdated: 10-min head start / 60-min game, multiple Hunters, a game-creator-drawn boundary not enforced, `HX-4291`-style codes, creator assigns every role by drag. Code: one Hunter, configurable timings, optional boundary penalties, 4-digit codes, players ready up.
- `openspec/specs/hunter-start-delay` says a 50 m threshold; code uses **25 m**. `openspec/specs/games` ties `EnableHunterBoundaryPenalty` to head-start movement; code uses it for the Hunter leaving the playfield and always applies the head-start penalty.
- `openspec/specs/player-status-tracking` describes a `PlayerStateMonitor` service; that class still exists but is **not registered** — the sweep does the timeouts.
- `openspec/specs/games` says a new game starts with an empty lobby; the creator actually joins as first participant.
- The website says a penalised player is shared "every 10 seconds"; broadcasts happen at the 30 s sweep (10 s is only the interval reported to the device).

## When the rules change

1. Change the domain (`Game.cs` / `GameConfiguration.cs`) and its unit tests first (**the-prey-server-feature**); propose it through OpenSpec.
2. Mirror it in the app (`GameSettings.kt`, HUDs, strings) — **the-prey-android-feature**, **the-prey-api-contract-sync**.
3. Update the website rules, FAQ, roles and home `phases` in EN and NL — **the-prey-website-content**, **the-prey-multilingual**, **the-prey-docs-sync**.
4. Update `docs/game-design/*.md` and this skill (including *Known drift*) in the same change.

## Verify

```powershell
# domain rules are covered by the Games tests
dotnet test server/src/Games/HexMaster.ThePrey.Games.Tests/
# find every place a constant is used before changing it
git grep -n "TagRangeMeters\|PenaltyDurationMinutes\|HunterDelayMovementThresholdMeters"
# website numbers that must match (EN + NL)
git grep -n "50 m\|25 m\|10 min\|16 players\|16 spelers" -- website/src/content
```

## Checklist

- [ ] Behaviour matches `Game.cs` / `GameConfiguration.cs`, not the old design docs.
- [ ] Constants used by name, never re-typed as magic numbers in new code.
- [ ] App option lists and defaults still satisfy the server invariants.
- [ ] Website (EN + NL), `docs/game-design` and this skill updated when a rule changed.
- [ ] Any new spec/doc vs code mismatch recorded under *Known drift*.
