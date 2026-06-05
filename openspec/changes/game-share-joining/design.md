## Context

The game lobby page (`game-lobby.page.ts`) already shows the `gameCode` prominently as a hero display. Joining currently requires navigating to a game by GUID — there is no flow where an external player can jump directly into a lobby from a link. The backend has a `POST /{id:guid}/lobby` join endpoint that takes a game GUID. There is no lookup-by-code endpoint.

The client is an Ionic/Angular app. The backend is ASP.NET Core 10 Minimal API, modular-monolith with CQRS.

## Goals / Non-Goals

**Goals:**
- Let any lobby participant share a join invitation via the native Web Share API.
- Add a `games/join/:code` deep-link route to the client that resolves the code and joins the lobby.
- Add a backend endpoint to look up a game by its alphanumeric code.

**Non-Goals:**
- Generating custom short URLs or QR codes.
- Handling the join flow for users who are not yet authenticated (Auth0 redirect is already handled globally by the `authGuardFn` guard).
- Modifying who is allowed to share (any lobby member can share, not just the owner).

## Decisions

### Decision 1: Share via Web Share API, not a copy-to-clipboard fallback

The Ionic app targets mobile devices where `navigator.share` is universally available. Implementing a clipboard fallback adds complexity for no practical benefit on mobile. If the API is unavailable the button will not appear.

**Alternative considered**: Always show a "Copy link" button. Rejected — the native share sheet is the better UX on mobile.

### Decision 2: Deep link uses game code, not game GUID

The URL `/games/join/ABCD1234` is human-readable and matches what is displayed on the lobby header. The GUID (`/games/{guid}/lobby`) is already used for in-app navigation after joining.

**Alternative considered**: Encode the GUID in the link and skip the lookup endpoint. Rejected — the game code is already the user-facing identifier, and the GUID is an internal detail that should not leak into invitation messages.

### Decision 3: Backend lookup endpoint returns the full GameDto

The join page needs to display the game name/code and potentially the playfield before the user confirms joining. Returning the full `GameDto` (already defined) avoids creating a new thin DTO and reuses existing serialisation.

**Alternative considered**: Return only `{ id, gameCode }`. Rejected — the join page would need a second request to get game details for a confirmation screen, which is unnecessary complexity.

### Decision 4: Join page auto-joins when the user is authenticated

On arrival at `/games/join/:code`, the page resolves the code, calls `JoinGame`, then redirects to `/games/{id}/lobby`. There is no separate "confirm join" step — the invitation message already explains what the user is doing.

**Alternative considered**: Show a confirmation screen before joining. Kept as an open question (see below), but the simpler auto-join is preferred for this iteration.

### Decision 5: New backend feature slice `GetGameByCode`

Following the vertical-slice / CQRS pattern already used in the project, a new `GetGameByCode` query + handler is added inside `HexMaster.ThePrey.Games/Features/GetGameByCode/`. The endpoint is registered in `GameEndpoints.cs` as `GET /games/code/{code}`.

## Risks / Trade-offs

- **Web Share API availability** → The share button is conditionally rendered using `*ngIf="canShare"` bound to `!!navigator.share`. No UX regression on unsupported platforms.
- **Game code collisions** → Game codes are assumed unique by the existing domain. If lookup returns multiple games for a code, the endpoint returns the most recently created one. This is a pre-existing domain concern.
- **Auto-join silently fails if already a member** → The backend returns a validation error when a player tries to join twice. The join page should catch this and redirect to the lobby rather than showing an error, since the user is effectively already "in".

## Open Questions

- Should the join page show a confirmation screen ("You're about to join game ABCD1234 — continue?") or auto-join immediately? Current decision: auto-join. Revisit if UX feedback suggests otherwise.
