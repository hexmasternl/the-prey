## Context

Boundary penalties already exist end-to-end. The game snapshot (`GameLiveState` / `GameLiveParticipant.PenaltyEndsAt`) carries each participant's penalty end time, and `GameHudViewModel` already resolves the local player's penalty (`LocalPenaltyEndsAt`, `_penaltyEndsAt`, `IsPenalised()`). Today that state is used only to switch the hidden GPS-ping cadence to a fixed 30-second bar — there is no visible cue that the player is penalised.

The HUD (`GameHudView`) is a bottom-anchored overlay injected into a `HudRegion` `ContentView` (`VerticalOptions="End"`) on both `PreyGamePage` and `HunterGamePage`. The app enforces two single sources of truth: all appearance via `Colors.xaml` + `Styles.xaml`, and all user-facing text via `AppResources.resx` (+ `AppResources.nl.resx`), consumed with `{loc:Translate}`. The alarming red `TpHunter` (`#ff2f1f`) already exists and is the established "danger" colour (used by `GamePenaltyWarning`). The HUD ticks once per second via an existing `Tick()` loop and re-seeds from each store snapshot in `ApplyState`.

## Goals / Non-Goals

**Goals:**
- Show an unmistakable, alarming top-of-screen banner ("PENALTY" + mm:ss countdown) whenever the local player is penalised, for either role.
- Drive the banner off the penalty state the HUD already computes; add no new data source, API call, or realtime event.
- Auto-show and auto-hide the banner from the clock, so it appears/disappears without needing a server event.
- Keep all appearance and text in the central resource files.

**Non-Goals:**
- No changes to penalty *rules*, duration, or how penalties are incurred (that is server-side and unchanged).
- No change to the existing ping-cadence penalty behaviour.
- No sound, haptics, or animation beyond a static alarming bar (can be a follow-up).
- No banner for penalties belonging to *other* participants.

## Decisions

### Decision: Surface penalty state as bindable properties on `GameHudViewModel`
Add two observable properties: `bool IsPenalised` (banner visibility) and `string PenaltyRemainingText` (mm:ss countdown). Both are updated from the two paths that already run: `ApplyState` (when a snapshot re-resolves `_penaltyEndsAt`) and `Tick` (each second). The countdown is computed as `_penaltyEndsAt - _timeProvider.GetUtcNow()`, reusing the existing `FormatDuration` helper and the existing `IsPenalised()` clock check.

- **Why**: The VM is the single source of penalty truth and is already unit-testable via `TimeProvider`. Binding keeps the view declarative.
- **Alternative considered**: Duplicate penalty tracking on `PreyGameViewModel` / `HunterGameViewModel` (the page VMs). Rejected — it would fork the penalty logic and diverge from the HUD's clock-driven regime.

### Decision: Host the banner in a top-anchored slot, driven by the HUD view model
The banner must sit at the top while the HUD panel stays at the bottom. Preferred approach: make `GameHudView` occupy the full region and lay out top banner + bottom panel, changing `HudRegion` from `VerticalOptions="End"` to `Fill` on both pages, with the empty middle kept `InputTransparent` so the map stays pannable. The banner row and panel row are the only input-opaque children.

- **Why**: Keeps the penalty UI co-located with the penalty state (the HUD VM), so both pages get it for free and there is one binding context.
- **Alternative considered**: Add a standalone banner directly on each page (like the existing spectator banner at `VerticalOptions="Start"`). Rejected as the primary approach because the page `BindingContext` is the page VM, not the HUD VM, so it would require plumbing penalty state onto the page VMs. Kept as a fallback if the full-bleed `HudRegion` change causes input-capture issues over the map.

### Decision: New style + reused colour, new localized caption
Add a `HudPenaltyBanner` style in `Styles.xaml` (full-width, centred, bold, `TpHunter` background / light text) and, if needed, a semantic colour alias in `Colors.xaml`; reuse the existing `TpHunter` red. Add a `Hud_Penalty` (="PENALTY") key to `AppResources.resx` and its Dutch equivalent to `AppResources.nl.resx`. The countdown value is bound from `PenaltyRemainingText`; only the caption is a static localized string.

- **Why**: Enforces the two single-sources-of-truth rules and matches the existing danger-colour convention.

## Risks / Trade-offs

- **Making `HudRegion` full-bleed could capture map touch input in the empty middle** → keep the middle spacer `InputTransparent`, and verify panning still works on both pages; fall back to the page-level standalone banner if it does not.
- **Countdown drift between the local tick and the server penalty end** → the countdown is recomputed from the absolute `PenaltyEndsAt` each tick (not decremented independently), so it self-corrects and hides exactly when the clock passes the end time, consistent with the existing `IsPenalised()` regime.
- **Banner could linger if a snapshot clears the penalty but no tick has fired yet** → `ApplyState` updates `IsPenalised`/`PenaltyRemainingText` too, so both the per-second tick and each snapshot converge the banner state.
- **Localization completeness** → add both English and Dutch keys in the same change; the app switches language live, so a missing key would surface immediately.
