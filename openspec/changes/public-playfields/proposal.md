## Why

The "Public" tab on the Playfields page is a stub that shows "Coming soon". Players have no way to discover shared playfields created by others, which is the foundation for joining existing games. The backend search endpoint (`GET /playfields/public?q=`) already exists; this change wires up the client.

## What Changes

- Replace the "coming soon" placeholder in the Public tab with a live search experience: `ion-searchbar` with 3-character minimum and debounce
- After 3 characters are typed, call `GET /playfields/public?q=<text>` and render results as a tappable list
- Tapping a public playfield navigates to the existing `/playfields/:id` detail page (view-only for non-owners)
- Add a `searchPublicPlayfields(query: string)` method to `PlayfieldsService`
- Guard edit controls on the detail page so the visibility toggle and "Set Area" button are hidden when the current user is not the owner

## Capabilities

### New Capabilities

- `public-playfield-search`: Debounced search for public playfields on the Public tab — searchbar, 3-char minimum, results list, navigation to detail

### Modified Capabilities

- `playfield-details`: Detail page must become read-only for non-owners — hide visibility toggle and Set Area button when `playfield.ownerId !== currentUserId`

## Impact

- **Client only** — `src/ThePrey/` Ionic/Angular app; no backend changes required (endpoint and visibility rules already implemented)
- Modify `src/app/playfields/playfields-list.page.html` and `.ts` — replace public tab stub
- Modify `src/app/playfields/playfields.service.ts` — add `searchPublicPlayfields` method
- Modify `src/app/playfields/playfield-detail.page.ts` — inject `UserStateService`, derive `isOwner` signal, conditionally render edit controls
- Requires `rxjs` debounce/switchMap for the search observable (already a dependency)
