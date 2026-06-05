## Why

Players need a way to start a new game session from the mobile app. The backend already supports game creation via POST `/games`, but the client has no UI for it — the "Play Now" button currently goes nowhere useful. This change adds the Game Create page so a player can configure and launch a game from their phone.

## What Changes

- New **Game Create** page (`/games/create`) in the Ionic/Angular app where an authenticated player selects a playfield and configures game settings before submitting a create request.
- Game configuration options presented as segmented controls with discrete choices:
  - Game duration: 30, 60, or 90 minutes (default 60)
  - Hunter delay time: 5, 10, or 15 minutes (default 10)
  - Endgame (final stage) duration: 5, 10, or 15 minutes (default 10)
  - Location reporting interval: 3, 5, or 10 minutes (default 5) — sent to server as seconds
  - Endgame location interval: 1, 3, or 5 minutes (default 3) — sent to server as seconds
- Playfield selection via the existing `PlayfieldSelectionPage` modal.
- "Create Game" button submits `POST /games` and navigates to the Game Lobby page on success.
- New route `/games/create` added to `app.routes.ts`.
- New `createGame` method added to `GamesService`.
- i18n keys added for English and Dutch.
- Home page "Play Now" button navigates to `/games/create` instead of `/play`.

## Capabilities

### New Capabilities

- `game-create-page`: Ionic/Angular page that lets an authenticated player configure and create a new game session, then navigate to the game lobby.

### Modified Capabilities

- `games`: The game creation requirement already exists in the backend spec. No requirement changes — this change only adds the client-side implementation that calls the existing endpoint.

## Impact

- `src/ThePrey/src/app/games/` — new `game-create.page.ts`, `game-create.page.html`, `game-create.page.scss`
- `src/ThePrey/src/app/games/games.service.ts` — new `createGame()` method
- `src/ThePrey/src/app/app.routes.ts` — new `/games/create` route
- `src/ThePrey/src/app/home/home.page.ts` — update `goToPlay()` to navigate to `/games/create`
- `src/ThePrey/src/assets/i18n/en.json` — new translation keys
- `src/ThePrey/src/assets/i18n/nl.json` — new translation keys
- No backend changes required (endpoint already exists)
