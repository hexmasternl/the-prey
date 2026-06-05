## Context

The Ionic/Angular client already has a `GamesService` with a `getActiveGame()` method and a `PlayfieldSelectionPage` modal. The backend exposes `POST /games` which accepts a playfield ID and a `GameConfiguration` object. The home page has a "Play Now" button that currently navigates to `/play` (a dead route). The playfield selection modal is already functional and reusable. All game configuration fields are fixed to small discrete option sets, so radio/segment controls are sufficient — no free-form input.

## Goals / Non-Goals

**Goals:**
- Deliver a single Ionic page at `/games/create` that lets a player pick configuration options and a playfield, then POST to the server.
- Reuse the existing `PlayfieldSelectionPage` modal without changes.
- Wire the home page "Play Now" button to the new route.
- Follow existing Ionic/Angular patterns in the codebase (standalone components, signals, TranslatePipe, `IonSegment`/`IonSegmentButton`).

**Non-Goals:**
- Validation beyond what the server enforces (client enforces only that a playfield is selected before enabling the button).
- Free-form text or numeric inputs — all options are from fixed lists.
- A "Game Lobby" page (that is a subsequent change; this change navigates to `/games/:id/lobby` after creation).
- Backend changes.

## Decisions

### Option controls: `IonSegment` vs `IonSelect`

**Decision:** Use `IonSegment` / `IonSegmentButton` for each configuration field.

**Rationale:** Each field has 3 options — small enough that all choices fit on screen as a segmented control, which is faster to tap than a dropdown. `IonSelect` would require an extra tap to open a picker. `IonSegment` also makes the current selection immediately visible.

**Alternative considered:** `IonSelect` — rejected because the extra interaction cost is not justified for 3-item sets.

### Interval unit display vs. storage

**Decision:** Display intervals in minutes in the UI; convert to seconds only when building the POST payload.

**Rationale:** The backend spec stores `DefaultLocationInterval` and `FinalLocationInterval` in seconds, but "3 minutes", "5 minutes", "10 minutes" are more human-readable labels. Converting at payload construction keeps the component model clean.

### Playfield selection: modal vs. inline list

**Decision:** Open `PlayfieldSelectionPage` as an `IonModal` (same pattern it already supports via `ModalController`).

**Rationale:** The selection page is already built as a modal with `confirm()` / `cancel()` methods. Reusing it requires zero changes to that page and keeps the create page compact.

### Navigation after creation

**Decision:** Navigate to `/games/:id/lobby` after a successful `POST /games`.

**Rationale:** The proposal specifies navigating to the Game Lobby page. The route `/games/:id/lobby` is the logical destination (to be created in a subsequent change). If that route does not yet exist, the navigation will land on a 404 within the app — acceptable as interim behaviour since the lobby page is the next deliverable.

### Error handling

**Decision:** Show a dismissible `IonToast` with a generic error message on HTTP failure; keep the form populated so the user can retry.

**Rationale:** Consistent with other pages in the app. The server returns validation errors for invalid configs, but since all valid option combinations satisfy the backend constraints (no endgame > game duration is possible given the constrained option sets), errors will typically be network failures.

## Risks / Trade-offs

- **Option combinations may violate server constraints** → The discrete sets are chosen so that no combination violates the rules (e.g., min endgame 5 min < min game duration 30 min; min hunter delay 5 min < min game duration 30 min). However if options are ever widened, this assumption breaks. Mitigation: add client-side validation that re-checks constraints before enabling Create.
- **`/games/:id/lobby` route does not yet exist** → Navigation succeeds but lands on a blank/404 screen. Mitigation: acceptable as interim behaviour; lobby page is next.
- **`GamesService.createGame()` is new surface** → No existing test coverage for the service method. Mitigation: the method is a thin HTTP wrapper; manual testing on the emulator is sufficient for now.

## Open Questions

- Should the "Create Game" button show a loading spinner while the request is in-flight, or is a simple disabled state enough? (Assumed: disabled + spinner for clarity.)
- Does the Game Lobby route exist yet, or should the post-create navigation target a placeholder? (Assumed: navigate to `/games/:id/lobby` regardless; lobby page follows in next change.)
