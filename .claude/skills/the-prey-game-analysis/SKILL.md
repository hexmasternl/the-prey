---
name: the-prey-game-analysis
description: How to obtain and analyse real game exports of The Prey (GET /games/export/today, GameExportDto) — the export schema, the feature flag and privacy rules around it, the questions to ask (per-player location timelines, report cadence vs configured ping intervals, gaps and signal loss, boundary violations and penalties, hunter–prey distance over time, tagging, game duration vs configuration) and a reusable standard-library Python script that summarises an export. Use whenever someone shares or asks about a game export or a file in analysis/, wants to know "what happened in that game", investigates a playtest bug (missed pings, wrong penalty, impossible tag, game ended too late or early), or wants to turn playtest findings into OpenSpec changes, rule updates or bug fixes.
metadata:
  author: the-prey
  version: "1.0"
---

# The Prey — Analysing Game Exports

Real playtests are the best test data we have. This skill covers getting an export, reading it, and turning what you find into changes. The rules you compare against are in **the-prey-game-rules**. When an export and that skill disagree, find out which one is wrong.

## 1. Getting an export

| What | Where |
|---|---|
| Endpoint | `GET /games/export/today[?date=yyyy-MM-dd]` → `IReadOnlyList<GameExportDto>` |
| Code | `server/src/Games/HexMaster.ThePrey.Games.Api/Endpoints/GameExportEndpoints.cs`, handler `server/src/Games/HexMaster.ThePrey.Games/Features/ExportGames/ExportGamesQueryHandler.cs`, mapping `ToExportDto` in `GameMappings.cs` |
| Selection | Games whose **`StartedAt`** falls in `[date 00:00 UTC, +1 day)` (`GameRepository.GetGamesStartedBetweenAsync`). Lobby-only/cancelled games are never exported. Without `date` it uses today (UTC) |
| Auth | **None**: `.AllowAnonymous()`, no bearer token |
| Gate | Feature flag `EnableDailyGamesExport`, **off by default** (`server/infrastructure/landing-zone/modules/app-config-values.bicep`). Off → `404`. Bad `date` → `400` validation problem |
| Retention | Games are hard-deleted 48 h after creation, so export within two days of the playtest |

Steps:

1. Ask the user to switch `EnableDailyGamesExport` on in Azure App Configuration (feature manager). Flags refresh at runtime, so no restart is needed. Locally (Aspire, no App Configuration) set `FeatureManagement__EnableDailyGamesExport=true` on the games API.
2. Fetch it through the gateway (route prefix `/games` → games API), e.g. `curl -s "https://<gateway-host>/games/export/today?date=2026-06-17" -o analysis/2026-06-17.json`. See **the-prey-local-dev** for local URLs.
3. Ask the user to **switch the flag off again** straight away.

**Privacy:** the export is unauthenticated and holds user ids, display names, profile picture URLs and full GPS tracks of real people. Never leave the flag on, never paste ids, names or coordinates into issues, PRs, OpenSpec files, commit messages or skills. Talk about "Hunter", "Prey-1", relative times (`+12:30`) and distances. The script below does this by default. Ask the user before committing a new export under `analysis/`.

## 2. Schema (`GameExportDto`)

```
[ {                                   // list; analysis/first-real-game.json holds one bare object
  id, gameCode ("4 digits"), playfieldId, ownerUserId,
  status ("Completed" | "InProgress" | "Started"), outcome ("PreysWin" | "HuntersWin" | "Undecided"),
  configuration: { gameDuration (min), hunterDelayTime (min), finalStageDuration (min),
                   defaultLocationInterval (s), finalLocationInterval (s),
                   enablePreyBoundaryPenalties, enableHunterBoundaryPenalty },
  createdAt, startedAt, endsAt (= startedAt + gameDuration), completedAt, cleanUpAfter,
  hunterUserId, preys: [userId],
  participants: [ {
    userId, displayName, profilePictureUrl, isReady, isHunter,
    state ("Active" | "Passive" | "Out" | "Tagged"),
    lastLocationAt, location {latitude, longitude},   // last BROADCAST position
    delayAnchorLocation, delayPenaltyApplied,          // hunter head-start check
    penalties: [ { id, endsAt } ],                     // no reason, no start time
    locations: [ { id, coordinate {latitude, longitude}, recordedAt, checked } ]
  } ]
} ]
```

Things the export does **not** contain, so you have to infer or fetch them:

- **Tag time and tagger** — only the final `state: "Tagged"`. Last prey reading and, for `HuntersWin`, `completedAt` bound it.
- **Penalty reason/start** — a penalty ending at `startedAt + hunterDelayTime + 10 min` is the head-start penalty; anything else is a boundary penalty that started 5 min before `endsAt`.
- **Playfield polygon** — fetch `GET /playfields/{playfieldId}` (authenticated, `PlayFieldDto.points`) and pass it to the script.
- **Passive/Out timestamps, broadcast history, realtime messages** — use Application Insights / OTel traces (`GameTick.ProcessGame` activity tags) for those (**the-prey-local-dev**, aspire-monitoring).

Timestamps: `recordedAt` is the GPS fix time sent by the phone, corrected by the app with an estimated server clock offset (`tracking/TrackingController.kt`); the server only uses its own clock when it is missing. A bad offset or a stale fix shows up as skew or out-of-order readings.

## 3. Run the script

```powershell
py -I .claude/skills/the-prey-game-analysis/scripts/analyze_game_export.py analysis/2026-06-17.json
py -I .claude/skills/the-prey-game-analysis/scripts/analyze_game_export.py analysis/2026-06-17.json --game 5386 --playfield playfield.json --gap-seconds 30
```

(`py` is the Windows launcher; use `python3 -I` elsewhere. `-I` keeps the run isolated.) Standard library only. Options: `--game <code|id>`, `--playfield <PlayFieldDto.json | [points]>`, `--gap-seconds N` (default 60), `--show-names` (local eyes only).

Per game it prints: configuration; a timeline relative to start (hunter release, final stage, scheduled end, actual completion and the inferred completion reason); per participant the reading count, first/last reading, median/p90/max interval, cadence verdict, long gaps, ≥ 5/7 min silences, unchecked readings, readings after completion, distance travelled and GPS jumps (> 15 m/s); the head-start drift vs the 25 m threshold and whether the penalty matched; penalties classified by kind; readings outside the polygon (with `--playfield`); and the hunter–prey distance trail with in-range samples and a consistency check for tagged Prey.

The rule constants at the top of the script mirror `Game.cs`. Update them in the same change when the domain changes.

## 4. Questions to ask

| Question | How to answer | Expected (current rules) |
|---|---|---|
| Did every phone keep reporting? | median/p90 interval, long gaps | ~5 s device cadence; exports from before the 5 s protocol (e.g. 2026-06-17) show the configured ping (180/300 s) instead |
| Did anyone lose signal? | gaps ≥ 5 min (Passive) / ≥ 7 min (Out) for Prey | Final `state` should match: a ≥ 7 min prey gap → `Out` |
| Did the game end on time? | completed − endsAt | 0–30 s late (one sweep) for `PreysWin`; earlier only when all Prey are Tagged/Out |
| Was the outcome right? | participant states vs outcome | `HuntersWin` ⇔ every Prey Tagged/Out |
| Did the Hunter wait during the head start? | drift from `delayAnchorLocation` before release | > 25 m ⇒ exactly one penalty ending release + 10 min |
| Did anyone leave the playfield? | `--playfield` outside count vs penalties | Penalty only when that role's flag is on; never stacked; 5 min each |
| Were tags plausible? | hunter–prey distance near the tagged prey's last reading | ≤ 50 m between latest readings at tag time |
| How close did the hunt get? | distance trail, closest approach | Tuning input for tag range, ping intervals, playfield size |
| Were settings sensible? | duration vs when it was decided, ping vs distances | Input for app defaults |
| Data hygiene | unchecked readings, readings after completion or before start, GPS jumps | Unchecked only for the last < 30 s; no readings after `completedAt` |

Large time skew in the distance section (hunter reading much older than the prey reading) means the distance is unreliable. Say so in your findings.

## 5. Report and feed back

Write a short findings summary: per game, what happened in one line, then numbered findings, each with evidence (relative times, distances, counts — no ids or names) and a classification:

| Finding is… | Action |
|---|---|
| Code doesn't do what the rules say (wrong penalty, late end, tag out of range accepted, missing timeout) | Bug: reproduce in a unit test in `server/src/Games/HexMaster.ThePrey.Games.Tests/` with a fixed `TimeProvider`, then fix (**the-prey-server-feature**). App-side: **the-prey-android-feature** |
| Rules work as designed but play badly (head start too short, 50 m too tight, pings too slow) | Rule change: OpenSpec proposal (`opsx:propose`, **the-prey-git-workflow**), then update **the-prey-game-rules**, `docs/game-design/`, the website (EN + NL) via **the-prey-docs-sync** |
| Device/OS behaviour (background kills, GPS jumps, battery) | App issue: tracking service, permissions, Play disclosure (**the-prey-play-store**) |
| Missing data for the analysis (no tag time, no penalty reason) | Export improvement: extend `GameExportDto` + mapping as a server slice, and update the schema section above |

Keep exports out of commits unless the user asks; when they do, they go in `analysis/` named by date or occasion.

## Verify

```powershell
# the script runs clean on both samples
py -I .claude/skills/the-prey-game-analysis/scripts/analyze_game_export.py analysis/first-real-game.json
py -I .claude/skills/the-prey-game-analysis/scripts/analyze_game_export.py analysis/2026-06-17.json
# export contract unchanged? (compare the DTO with the schema section above)
git log -1 --format=%cd -- server/src/Games/HexMaster.ThePrey.Games.Abstractions/DataTransferObjects/GameExportDto.cs
```

## Checklist

- [ ] Export fetched while the flag was on, and the flag switched off again.
- [ ] Analysis compared against **the-prey-game-rules**, not against memory or old docs.
- [ ] Old-protocol exports recognised (cadence = configured ping, not 5 s) before calling anything a bug.
- [ ] Findings contain no user ids, names, profile URLs or raw coordinates.
- [ ] Each finding classified and routed: bug, rule change (OpenSpec), app issue or export improvement.
- [ ] Script constants and schema section updated if the domain or `GameExportDto` changed.
