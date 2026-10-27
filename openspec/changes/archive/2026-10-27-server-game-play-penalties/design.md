# Design

## Context

See proposal.md (Why) for the root cause. The parts that already work and stay as they are:

- `Game.TryApplyBoundaryPenalty` applies a 5-minute penalty. It does not stack while a penalty is active and skips Tagged/Out players. `GameSweepProcessor.ApplyBoundaryPenaltiesAsync` checks every reading consumed in a sweep (or the last known position) against the playfield polygon and broadcasts `prey-updated` / `penalized` / `left-playfield`.
- The head-start penalty (`AssessHunterHeadStartPenalty`) was never gated by a flag. It still runs every sweep.
- The app already renders penalties: the Prey role pill (`PreyHud`), the Hunter clock band and penalised-preys tile (`HunterHudState`), and the map marker pulse (`MarkerStyle`). All of them go through `isPenaltyActive(hasActivePenalty, penaltyEndsAt, serverNow)`, which prefers `penaltyEndsAt` and falls back to the flag.
- The server never emits `penalty-cleared`. Clients expire penalties from `penaltyEndsAt` on a ticking server clock (`GameScreen`, once per second).

So once penalties are applied, they reach the app through the live `prey-updated` message. Two things are missing: the server applies no boundary penalties at all, and after a snapshot the app has no end time.

## Goals / Non-Goals

**Goals:**
- Every in-progress game penalises a Prey or Hunter who leaves the playfield, including games created by app versions already in the field.
- A penalty is visible right after a full refresh and ends on time without a message.

**Non-Goals:**
- No setting for the game creator to turn boundary penalties off.
- No change to penalty length, the 10 s penalised reporting interval, the 30 s sweep, or the head-start penalty.
- No indicator for the Prey of the Hunter's penalty, and no per-penalty reason in the snapshot.
- No `penalty-cleared` emission. Expiry stays client-side from the end time.
- The columns stay in the database (no migration).

## Decisions

**1. Server ignores the flags instead of the app turning them on.**
The sweep stops reading `EnablePreyBoundaryPenalties` / `EnableHunterBoundaryPenalty`. *Alternative:* only change the app to send `true`. Rejected: games created by installed older versions would still never penalise, and the decision is a game rule, which belongs on the server.

**2. Keep the flags on the wire, report them as `true`.**
Requests still bind both fields, so older apps' bodies are valid. `GameConfiguration` keeps storing what was sent, and nothing reads it. `GameMappings.ToDto(GameConfiguration)` returns `true` for both, so every reader (lobby, `configuration-changed`, `/gamestate`) sees the actual behaviour. *Alternative:* remove the fields. Rejected: that breaks older apps' deserialisation and request bodies. Mark the domain properties and DTO fields with XML doc remarks ("ignored; boundary penalties always apply"), not `[Obsolete]`, which would turn the warnings into build noise in the mapping and Postgres configuration.

**3. `ParticipantDto.PenaltyEndsAt` (nullable, last positional parameter with default `null`).**
Filled from the existing `GameParticipant.ActivePenaltyEndsAt(now)` in `GameMappings.ToDto(GameParticipant, now)`. Because `ParticipantDto` is also the `participant-changed` payload, that message carries it as well at no extra cost. *Alternative:* a separate penalty list on `GameStatusDto`. Rejected: it duplicates the participant collection the app already reduces over. The existing `hasActivePenalty` stays for older apps.

**4. App: parse, don't patch.**
`ParticipantStateDto.penaltyEndsAt` is already a field on the Kotlin DTO (today only filled by the reducer). It now deserialises from the server; only its KDoc changes. `isPenaltyActive` already prefers the end time, so no HUD or marker code changes. `CreateGame.toRequest()` sends `true` for both flags. The value is cosmetic but keeps the request honest.

**5. Tests.**
Server: in `GameSweepProcessorTests`, replace the "flag disabled → no penalty" cases with "flag false → still penalised" for Prey and Hunter, and keep the no-stacking, Tagged/Out and <3-point polygon cases. In the mapping tests, cover flags reported `true` and `PenaltyEndsAt` set or null. App: `GameStateDtoTest` decodes `penaltyEndsAt`, `CreateGameRepositoryTest` asserts the flags are `true`, and `ActiveGameStateTest`/`PreyHudStateTest`/`HunterHudStateTest` cover a snapshot penalty that has expired and one that is still running.

## Risks / Trade-offs

- [Players in games already in progress are suddenly penalised after deploy] → Intended. The next sweep only checks new readings or the last known position, so at most one penalty per player is applied when the deploy lands.
- [A GPS reading that jitters just outside the edge gives a penalty] → Existing behaviour whenever the flags were on. Out of scope here. If playtests show it, a tolerance margin can be a follow-up change.
- [The flags' meaning changes silently for API consumers] → The only client is the Android app. Documented in `docs/api/endpoints.md` and the game-rules skill.
- [Old app shows a penalty until the next resync after a refresh] → Older apps ignore `penaltyEndsAt`. They still get the live `penalized` message with its end time, so only the post-refresh case lags, as it does today.

## Migration Plan

Deploy the Games API through the normal `games.yml` pipeline. No database migration. Ship the app change on the internal track as usual. The PR is labelled `update:optional`, because older apps keep working. Rollback: redeploy the previous Games image, which makes the stored flags (all `false` for Kotlin-app games) gate penalties again.
