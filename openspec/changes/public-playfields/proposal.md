## Why

Players can manage their own private playfields, but there is no way to discover playfields created by others. Adding a public search tab to the existing playfields screen lets players find and preview community-made playfields without cluttering their own list or requiring a separate navigation destination.

## What Changes

- `PlayfieldsPage` gains a two-tab layout: **Private** (default, existing behaviour) and **Public** (new)
- The Private tab is unchanged: shows the user's own playfields with fetch-on-load, offline fallback, create-new, tap-to-detail, and swipe-to-delete
- The Public tab shows a search box; typing ≥ 3 characters triggers a debounced server query for public playfields
- Tapping a public playfield opens `PlayfieldDetailsPage` in read-only mode (non-owners cannot edit)
- Public playfield results are not stored in local cache

## Capabilities

### New Capabilities

- `public-playfield-search`: Debounced search for public playfields — search entry, minimum-character gate, server query, results list, read-only detail navigation, and no-results/error states

### Modified Capabilities

<!-- `playfield-list` is not yet archived to openspec/specs/ — no delta spec required.
     The Private tab preserves all existing playfield-list requirements unchanged;
     the structural change (tabs) is implemented entirely within PlayfieldsPage. -->

## Impact

- **App layer**: `PlayfieldsPage` refactored to host a custom two-tab header + `Grid`-switched content views; new `IPlayfieldService.SearchPublicPlayfieldsAsync(string query)` method
- **API**: `GET /playfields/public?q={query}` — no local caching of results
- **Navigation**: `PlayfieldDetailsPage` gains an `?readonly=true` query property to suppress edit controls for non-owners
- **No new dependencies**: debounce implemented with `CancellationTokenSource` + `Task.Delay`; no extra NuGet required
