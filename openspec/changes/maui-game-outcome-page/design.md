## Context

The MAUI app (`src/HexMaster.ThePrey.Maui.App`) already drives a full gameplay loop: a lobby, a gameplay map, and an in-game HUD (`GameHudViewModel`). The HUD already knows when a game ends — a status poll returning `Completed` (`GameStatusOutcome.Completed`) flips `HasEnded`, tears the HUD down, and raises a parameterless `GameEnded` event for the host page to act on. The real-time seam also already models the end: `GameStreamEvent.GameEnded(string? Outcome, int? SurvivorCount)`.

What is missing is the destination. When `GameEnded` fires there is no screen to show; the player is stranded on a frozen map. This change adds that screen.

Relevant existing seams and constraints this design builds on:

- **Navigation** is Shell-route based. Routes are registered in `AppShell.xaml.cs`; view models navigate through thin interfaces (e.g. `IMenuNavigator` → `Shell.Current.GoToAsync`) so they stay MAUI-free and unit-testable. DI wiring lives in `MauiProgram.cs`.
- **Styling** must go through two single sources of truth: central `Resources/Styles/Colors.xaml` + `Styles.xaml` for all appearance, and `Resources/Strings/AppResources.resx` (+ `AppResources.nl.resx`) for all text, consumed via `{loc:Translate}`. The app switches language live at runtime. Pages carry no inline styling and no hard-coded strings (enforced by the maui-styling-expert guidance).
- **Completed-game gotcha** (project memory `game-end-broadcast-and-spectator`): `GET /games/{id}/status` throws / no longer serves a game once it is `Completed`. Missed-end and post-end recovery must instead read the full record via `GET /games/{id}` (getGame) and inspect `Status == "Completed"` plus participant states.
- **Spectator rule**: a caught prey stays connected as a spectator and must also receive the end and see the outcome — being tagged earlier does not skip this screen.

## Goals / Non-Goals

**Goals:**
- A full-screen, role- and result-aware outcome page shown to every participant when the game ends.
- Correct win/lose determination for hunter, surviving prey, and caught prey, with the winning side and end reason named.
- Distinct, polished victory vs. defeat presentation, fully localized (English + Dutch), sourced entirely from central design + string resources.
- A single close action that returns to `HomePage` and clears the finished game from the back stack.
- Unit-testable win/lose logic and view model.

**Non-Goals:**
- No backend changes. The outcome is derived from data the backend already exposes (`GET /games/{id}` and the existing game-ended signal).
- No post-game statistics, scoreboards, per-player timelines, rematch, or sharing — this is the conclusion screen only.
- No changes to how the game *ends* server-side or how the HUD *detects* the end; this change consumes the existing `GameEnded` signal.
- Building the gameplay play pages themselves (hunter/prey) — those are separate in-flight changes. This change provides the outcome page and the navigation seam they call.

## Decisions

### Decision: A dedicated Shell route + page, navigated to on game-ended

Add an `outcome` Shell route resolving to `OutcomePage`, and an `IOutcomeNavigator` seam with a Shell-backed implementation. When the gameplay page's game-ended signal fires, it calls `IOutcomeNavigator.GoToOutcomeAsync(gameId, isHunter)`.

- **Why**: Mirrors every other MAUI screen in the app (route registration + interface-backed navigation), keeps view models MAUI-free, and lets the outcome page own its own lifecycle independent of the gameplay page being torn down.
- **Alternative considered**: An in-place overlay on the gameplay page. Rejected — it couples the outcome to the gameplay page's lifetime, complicates the "clear the game from the back stack" requirement, and makes the celebratory full-screen treatment harder.

### Decision: The OutcomeViewModel resolves the result itself from the completed game record

The page is navigated to with the minimum the caller reliably knows: `gameId` and the local player's role (`isHunter`). `OutcomeViewModel` then fetches the completed record via `IGameApiClient` (`GET /games/{id}`) and runs a pure resolver to compute the result.

- **Why**: Self-contained and resilient. Because `GET …/status` no longer serves a completed game (the memory gotcha), the authoritative post-end source is the full record. Resolving inside the VM means the page works whether it was reached from a live end signal or from a cold missed-event recovery, and it avoids threading a fragile outcome payload through Shell navigation query parameters.
- **Alternative considered**: Pass a fully-formed outcome descriptor (win/lose, side, reason, survivor count) as navigation parameters from the gameplay page. Rejected as the primary path — Shell query parameters are stringly-typed and the gameplay page's last known state may be stale at the exact moment of the end; the record read is the source of truth. (The real-time `GameEnded(Outcome, SurvivorCount)` may still be used as a fast hint, but the record is authoritative.)

### Decision: A pure `GameOutcomeResolver` mapping inputs → result

Introduce a small pure type (e.g. `GameOutcomeResolver` returning a `GameOutcome` record: `LocalPlayerWon`, `WinningSide` ∈ {Hunter, Preys}, `EndReason` ∈ {AllPreysCaught, TimeExpired}, `SurvivingPreyCount`). Inputs: `isHunter`, the local player's final participant state, the surviving-prey count, and the end reason (derived from whether any prey survived).

- **Why**: The win/lose matrix (hunter vs. prey × all-caught vs. time-up × survived vs. caught) is the one piece of real logic here and must be tested exhaustively. A pure function keeps it free of HTTP, MAUI, and time.
- Result matrix:
  - All preys caught → hunter wins; every prey loses.
  - Time expired with ≥1 survivor → surviving preys win; the hunter loses; a prey who was already caught loses (only survivors share the win).

### Decision: Victory/defeat presentation via named styles + a boolean-driven visual state

Add victory and defeat color tokens to `Colors.xaml` and named styles to `Styles.xaml`; the page selects between them from the VM's `LocalPlayerWon` (via style keys / triggers / a small value converter bound to the boolean). All text comes from new `AppResources` keys consumed with `{loc:Translate}`, with parallel entries in `AppResources.nl.resx`.

- **Why**: Enforces the two-single-sources-of-truth rule and live language switching. Keeps the page declarative and inspectable (no literals).
- **Alternative considered**: Two separate pages (WinPage/LosePage). Rejected — duplicates layout and localization wiring; one page with a visual-state switch is DRYer.

### Decision: Close clears the game stack back to HomePage

The close command navigates to the `HomePage` main-menu route using an absolute/reset navigation so the outcome, gameplay, and lobby pages leave the back stack; the platform back gesture must not return to the finished game.

- **Why**: The finished game is terminal — the player should land cleanly on the main menu with no way back into a dead game.

## Risks / Trade-offs

- **[The gameplay play pages that trigger the hand-off are still in-flight]** → This change ships the `OutcomePage` + `IOutcomeNavigator` + route so it is independently complete and testable; the actual `GoToOutcomeAsync` call is wired from the play pages / HUD host where they exist. The navigator and page do not depend on those pages existing.
- **[`GET /games/{id}` fails at the moment the outcome is shown]** → Degrade gracefully: show a neutral "game over" state that still offers the close/return-to-menu action, so the player is never trapped. Optionally seed from the real-time `GameEnded` hint when present.
- **[Determining a prey's "survived vs. caught" fate from the record]** → Rely on the participant `State` in the completed record (`Tagged`/`Out` = caught, otherwise survived), matched to the local user id — the same signal the gameplay map already uses to grey caught preys. If states are ambiguous, fall back to `SurvivorCount`/end-reason for the side, and treat the local prey conservatively.
- **[Double navigation / re-entrancy]** → The end can be observed more than once (a late poll plus the real-time event). Guard the hand-off so the outcome page is navigated to at most once per game (the HUD already raises `GameEnded` a single time; the navigator/host should be idempotent).
- **[Live language switch on a full-screen result]** → Because all text uses `{loc:Translate}` bindings, a runtime language change re-renders correctly without recreating the page.

## Open Questions

- Should the surviving-prey count be shown to the **losing** hunter as well (e.g. "3 preys escaped"), or only celebrated on the preys' victory screen? Leaning toward showing it on both for context.
- Does the design language want motion (a confetti/pulse animation on victory), and if so is a central animation resource in scope now or a follow-up polish pass? Default: a tasteful static treatment first, animation optional.
- Is the local player's role (`isHunter`) always reliably known at hand-off, or should the VM also derive it from the record's `HunterUserId` vs. the current user id as a fallback? Leaning toward deriving from the record to be robust.
