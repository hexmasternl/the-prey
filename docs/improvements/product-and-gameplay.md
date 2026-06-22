# Improvements — Product & Gameplay

Opportunities to deepen the game and grow retention. See [game-design/game-mechanics.md](../game-design/game-mechanics.md) for current rules.

## 1. Post-game replay — Impact: High · Effort: S–M

Location history is already captured (see `analysis/*.json`). Replaying a finished game is high delight for relatively low cost.

- Animated playback of prey tracks + hunter path + tag moments on the playfield map.
- Per-player stats (distance covered, time survived, closest call).
- Shareable highlight (ties into the existing share plugin).

## 2. Multiple hunters & team modes — Impact: High · Effort: L

The model is currently **one** hunter vs. many prey (`POST /games/{id}/hunter` designates one). Variety drives replay value.

- Multi-hunter mode (a hunter team that shares the prey radar).
- Team-vs-team or "infection" mode (a tagged prey becomes a hunter).
- These touch the engine's tagging, win conditions, and event payloads — plan as an engine change, not just UI.

## 3. Rule presets & tunable matches — Impact: Med · Effort: M

Timing/penalty/delay windows live in the domain. Expose curated presets so owners can shape a match.

- Presets like "Quick (15 min)", "Classic", "Marathon"; difficulty via head-start length, hunter delay, and broadcast cadence.
- Validate in the lobby `UpdateGameConfig` path; keep the server authoritative.

## 4. Safe zones & power-ups — Impact: Med · Effort: L

Add tactical texture inside the playfield.

- Timed safe zones where prey can't be tagged; limited-use prey "cloak" (skip one broadcast) or hunter "ping" (one extra location reveal).
- Implemented as engine-side modifiers to existing `PlayerState`/broadcast logic.

## 5. Accounts, progression & leaderboards — Impact: Med · Effort: M

Auth0 identities exist but there's no progression layer.

- Lifetime stats, badges, and per-area/global leaderboards.
- Friends and recent-players lists to make re-matching easy.

## 6. Pre-game readiness & onboarding — Impact: Med · Effort: S

The hunt depends on every player having background GPS working — a single mis-permissioned phone degrades the game.

- A lobby readiness check that verifies location permission + a recent fix before allowing "ready".
- First-run onboarding that explains roles, boundaries, and the background-location requirement.

## 7. Notifications for absent players — Impact: Med · Effort: M

Real-time is delivered to connected clients; a backgrounded/closed app misses key moments.

- Push notifications (FCM/APNs) for "game starting", "you've been located", "you were tagged", "game over" when the app isn't foregrounded — bridging the gap the Notifications module already conceptually owns.

## 8. Social / invite improvements — Impact: Low–Med · Effort: S

Join-by-code and deep links exist; reduce friction further.

- Rich share cards (map preview + code), QR codes for in-person lobbies, and "invite again" for recent groups.

## 9. Public playfield discovery — Impact: Low–Med · Effort: M

Public playfield search exists (name prefix). Turn it into discovery.

- "Playfields near me", popularity/ratings, and curated community areas — pairs with the [spatial indexing](./backend-and-architecture.md#6-spatial-indexing-for-public-playfields-impact-lowmed--effort-m) backend idea.
