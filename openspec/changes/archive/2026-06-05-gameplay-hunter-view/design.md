## Context

The hunter gameplay view is the second of two in-game screens (the first is the prey view, delivered by the `gameplay-prey-view` change). The backend already exposes two game-related endpoints introduced by that change: `GET /games/{gameId}/status` (participant-scoped HUD snapshot) and `GET /games/{gameId}/stream` (SSE push channel). This change builds the hunter-side frontend page and makes two targeted, additive extensions to those endpoints so the hunter can see prey positions.

The Ionic/Angular client is at `src/ThePrey`. The existing prey-view page (`game-prey.page`) and `GamesService` provide the pattern to follow. Leaflet is already used for the map; the style guide (`designs/hunter-gameplay-view.html`) defines the hunter visual theme (red `--hunter` as the accent, green `--signal` for the self-dot).

## Goals / Non-Goals

**Goals:**
- New `GameHunterPage` with full-screen Leaflet map, playfield polygon overlay (red tint), hunter self-dot (green pulsing), prey blips (red flashing), and a 4-cell HUD panel
- Additive extension of `GameStatusDto` with a `Participants` array (role + last known GPS) so the hunter can render prey dots on load
- Additive extension of the SSE stream: emit `participant-located` events to the connected hunter when any prey records a location
- App router directs InProgress hunter participants to `GameHunterPage`

**Non-Goals:**
- Showing prey names or identities on the map (only anonymous blips)
- Hunter-to-prey SSE events (already covered by prey-view change)
- Nearest-prey distance calculation on the server (computed client-side from GPS coordinates)
- Any changes to game state transitions or penalty application

## Decisions

### Decision 1: Additive `Participants` field on `GameStatusDto`

The `GameStatusDto` gains an optional array of `ParticipantSnapshotDto` objects, each carrying `Role`, nullable `Latitude`, and nullable `Longitude`. The field is populated for both hunter and prey callers but the hunter view is the primary consumer. Prey callers may ignore it.

**Alternative considered**: Separate endpoint (`GET /games/{id}/participants`) — rejected because it adds a round-trip on every poll cycle; an additive field on the existing status DTO costs nothing for prey callers and keeps the polling model simple.

### Decision 2: SSE prey→hunter broadcast via existing `IGameEventBus`

The existing `IGameEventBus` already sends `participant-located` events for the hunter's location to prey clients. We extend the same mechanism: when a prey records a location, publish a `participant-located` event and deliver it to the hunter's SSE connection. The event schema is unchanged (`participantRole`, `latitude`, `longitude`).

**Alternative considered**: Dedicated `IHunterEventBus` — rejected because the event structure is identical; splitting would duplicate infrastructure for no gain.

### Decision 3: Hunter blip renders last known position only

The hunter map renders the most recent location received, either from the status poll or from an SSE event. There is no interpolation or predictive movement. When a prey has no recorded location yet (null coordinates from status), that blip is hidden until the first SSE update.

### Decision 4: Frontend routing guard mirrors prey-view pattern

A route guard checks the active game's status and the authenticated user's role. If role is Hunter and state is InProgress, it activates `GameHunterPage`. If role is Prey, it activates `GamePreyPage`. This is the same guard pattern used for the prey view; only the role check differs.

## Risks / Trade-offs

- **Stale prey positions**: The hunter sees the last reported GPS position, which may be seconds or minutes old depending on the prey's reporting interval. This is intentional game design (the `FinalLocationInterval` shortens the interval in the endgame). → No mitigation needed; expected behavior.
- **SSE scale**: One SSE connection per active participant. With small expected player counts (<20/game) this is not a concern. → If player counts grow, revisit with WebSockets or a broker.
- **Null coordinates on initial load**: Preys who have not yet reported a position return null coordinates. The frontend must guard against rendering a blip at (0,0). → Client skips rendering blips with null coordinates.

## Open Questions

- Should the `Participants` snapshot filter out preys who have never reported (null coordinates) at the API level, or return them and let the client decide? Current decision: return them with null coordinates; client filters.
- Should the HUD "nearest prey" distance cell be hidden when no prey positions are known (all null)? Decision: show `--` placeholder text.
