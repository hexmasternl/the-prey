## 1. Service & Routing

- [x] 1.1 Add `createGame(playfieldId: string, config: GameConfiguration)` method to `GamesService` that POSTs to `/games` and returns the created game DTO
- [x] 1.2 Define `GameConfiguration` and `CreateGameRequest` interfaces in `games.service.ts` (or a co-located `game.model.ts`)
- [x] 1.3 Add `/games/create` route to `app.routes.ts` pointing to the new `GameCreatePage` (lazy-loaded)

## 2. Game Create Page

- [x] 2.1 Create `src/ThePrey/src/app/games/game-create.page.ts` as a standalone Ionic component with signals for each config value and the selected playfield
- [x] 2.2 Create `src/ThePrey/src/app/games/game-create.page.html` with `IonSegment`/`IonSegmentButton` controls for all five configuration parameters and pre-selected defaults
- [x] 2.3 Create `src/ThePrey/src/app/games/game-create.page.scss` with page-specific styles
- [x] 2.4 Add a "Select Playfield" button that opens `PlayfieldSelectionPage` as a modal and displays the confirmed playfield name
- [x] 2.5 Disable the Create Game button until a playfield is selected; show a loading spinner on the button while the POST is in-flight
- [x] 2.6 On successful creation navigate to `/games/:id/lobby`; on error show a dismissible `IonToast` with a generic error message
- [x] 2.7 Apply `authGuardFn` to the `/games/create` route in `app.routes.ts`

## 3. i18n

- [x] 3.1 Add all required `GAME_CREATE.*` translation keys to `src/ThePrey/src/assets/i18n/en.json` (page title, section labels, option labels, button labels, error message)
- [x] 3.2 Add matching Dutch translations to `src/ThePrey/src/assets/i18n/nl.json`

## 4. Home Page Update

- [x] 4.1 Update `goToPlay()` in `home.page.ts` to navigate to `/games/create` instead of `/play`
