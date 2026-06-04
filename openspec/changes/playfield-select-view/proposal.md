# Playfield Select View

## Why

Starting a new game requires the player to choose the playfield the game will be played on, but the app has no way to pick one: the upcoming create-game view needs a reusable selection screen that offers the player's own (synced) playfields plus discoverable public playfields. Building it now unblocks the Games app UI work.

## What Changes

- New MAUI page `PlayfieldSelectPage` that lets the user pick exactly one playfield and hand the selection back to its caller (the future create-game view).
- Default state shows the locally cached playfields whose sync state (`IsSynchronized`) is `true`; unsynced local playfields are shown as non-selectable (they may differ from the server copy) or hidden from the list.
- A search field at the top: when at least 3 characters are entered, the device queries the server and the list becomes a hybrid of matching local private playfields (owned by the current user) and matching public playfields, de-duplicated by id.
- Tapping a list item marks it as the selected playfield; a Select button enables only when a selectable playfield is selected and confirms the choice.
- Selection is passed back via a singleton selection context (mirroring the existing `PlayfieldEditingContext` pattern), so the create-game view can consume it when it is built.
- **Server**: implement the missing `GET /playfields/public?q={query}` search endpoint (the app client method `SearchPublicPlayfieldsAsync` already targets it), including a `SearchPublicPlayFields` query/handler and a repository search method.

## Capabilities

### New Capabilities

- `playfield-selection`: the playfield select view — local synced list, hybrid search (≥3 characters), selection rules (synced-only), Select button gating, and returning the selection to the caller.
- `public-playfield-search-api`: server-side search of public playfields by name (`GET /playfields/public?q=`), authenticated, returning playfield summaries.

### Modified Capabilities

<!-- none — the games capability spec is unchanged; the create-game view that consumes the selection is a separate future change -->

## Impact

- **App** (`src/App/ThePrey.Application.App/`):
  - New `PlayfieldSelectPage` (XAML + code-behind), registered route in `AppShell`, transient DI registration in `MauiProgram.cs`.
  - New singleton `PlayfieldSelectionContext` service to carry the selected playfield back to the caller.
  - Reuses `PlayfieldCacheService` (local list), `IPlayfieldService.SearchPublicPlayfieldsAsync` (server search), and the existing 3-character/debounce search pattern from `PlayfieldsPage`.
  - New localized strings in `AppResources.resx` / `AppResources.nl.resx` + `AppLocalizer` properties.
- **Server** (`src/PlayFields/`):
  - New feature slice `SearchPublicPlayFields` (query + handler + OTel instrumentation) in `HexMaster.ThePrey.PlayFields`.
  - New `IPlayFieldRepository` search method and Table Storage implementation in the data adapter.
  - New endpoint on the `/playfields` group in `HexMaster.ThePrey.PlayFields.Api`.
  - Unit tests in `HexMaster.ThePrey.PlayFields.Tests`.
- **Dependencies**: none new; overlaps with the in-progress `public-playfields` change only by closing the server-endpoint gap that change left open.
