## Context

The game lobby page (`game-lobby.page.ts`) already shows the `gameCode` prominently as a hero display and the `id` (GUID) is available via the route. Joining currently requires navigating to a game by GUID — there is no flow where an external player can jump directly into a lobby from a link. No backend changes are needed since `POST /games/{id}/lobby` already handles joining.

The client is an Ionic/Angular app. The backend is ASP.NET Core 10 Minimal API, modular-monolith with CQRS.

## Goals / Non-Goals

**Goals:**
- Let any lobby participant share a join invitation via the native Web Share API.
- Add a `games/join` route with a `gameId` query parameter that presents the user with a code-entry form.
- The user must type the 8-digit game code to confirm and execute the join — no auto-join.

**Non-Goals:**
- Adding any new backend endpoints.
- Generating custom short URLs or QR codes.
- Handling the join flow for users who are not yet authenticated (Auth0 redirect is already handled globally by the `authGuardFn` guard).
- Restricting sharing to the game owner (any lobby member can share).

## Decisions

### Decision 1: Share via Web Share API, no clipboard fallback

The Ionic app targets mobile devices where `navigator.share` is universally available. The button is hidden when the API is absent via a `canShare` computed signal. No UX regression on unsupported platforms.

**Alternative considered**: Always show a "Copy link" button. Rejected — native share sheet is the better UX on mobile.

### Decision 2: Deep link uses game ID (GUID), not the game code

The share URL is `/games/join?gameId=<uuid>`. The game GUID is already available on the lobby page and uniquely identifies the game. The code travels in the message text, not the URL — it is what the recipient types on the join page.

**Alternative considered**: Encode the code in the URL path (e.g. `/games/join/:code`). Rejected — would require a lookup-by-code backend endpoint and couples the URL to a human-readable identifier. Keeping the GUID in the URL and requiring manual code entry adds an intentional friction layer that prevents casual link-sharing without the code.

### Decision 3: Manual code entry on the join page (no auto-join)

The join page at `games/join?gameId=<id>` displays a single text input for the 8-digit game code. On submit, the page validates the entered code against the game (fetched via `GET /games/{id}`) and, if it matches, calls `POST /games/{id}/lobby`. This means the recipient cannot join without knowing the code, even if they have the link.

**Alternative considered**: Auto-join on page load. Rejected — the user explicitly requested manual code entry. It also serves as a guard against accidental joins from stale links.

### Decision 4: Code validation is client-side before the join call

The join page fetches the game by ID first (`GET /games/{id}`), compares the entered code to `game.gameCode`, and only calls the join endpoint if they match. This avoids a round-trip join attempt with a wrong code and gives a clear "incorrect code" error on mismatch.

**Alternative considered**: Submit code to backend for validation. Rejected — the code is already on the `GameDto`; a separate validation endpoint adds backend complexity for no gain.

### Decision 5: No new backend feature slice

The existing `GET /games/{id}` and `POST /games/{id}/lobby` endpoints cover everything the join page needs. No new query handlers, no new endpoints, no data adapter changes.

## Risks / Trade-offs

- **Web Share API availability** → Share button is conditionally rendered; no regression on unsupported platforms.
- **Game code exposed in GameDto** → The `gameCode` field is already returned by `GET /games/{id}` to any authenticated user. Accepting that any authenticated user who knows a game GUID can look up its code is an existing trust boundary, not a new one.
- **Already-a-member error on join** → The backend returns a validation error when a player tries to join twice. The join page catches this and redirects to the lobby rather than showing an error.
- **Stale deep links** → A link to a game that has already started will let the user through to the join page (code entry), but the backend join call will reject it. The page must handle this error and show a meaningful message.
