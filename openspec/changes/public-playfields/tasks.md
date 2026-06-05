## 1. API Service Extension

- [x] 1.1 Add `searchPublicPlayfields(query: string): Promise<PlayFieldSummaryDto[]>` to `PlayfieldsService` calling `GET /playfields/public?q=<query>`

## 2. Public Tab — Search UI

- [x] 2.1 Add a `searchQuery$` Subject and `publicResults` signal to `PlayfieldsListPage`
- [x] 2.2 In `ngOnInit` (or equivalent lifecycle), subscribe to `searchQuery$` with `debounceTime(400)`, `distinctUntilChanged`, `filter(v => v.length >= 3)`, and `switchMap` to call `searchPublicPlayfields`; store results in `publicResults` signal; cancel on component destroy
- [x] 2.3 Replace the "coming soon" placeholder in the `@case ('public')` block with: `ion-searchbar` bound to `searchQuery$`, a hint paragraph when query length < 3, a loading spinner while in-flight, an "No public playfields found" message when results are empty, and an `ion-list` of `ion-item` entries showing playfield names when results exist
- [x] 2.4 Wire `ion-item` tap in the public results list to navigate to `/playfields/:id`
- [x] 2.5 Import `IonSearchbar` in the `PlayfieldsListPage` component imports array

## 3. Detail Page — Owner Guard

- [x] 3.1 Inject `UserStateService` into `PlayfieldDetailPage`
- [x] 3.2 Add `isOwner = computed(() => !!this.playfield() && this.playfield()!.ownerId === this.userState.profile()?.userId)` signal
- [x] 3.3 Wrap the visibility `ion-item` with `@if (isOwner())` so it is only rendered for the owner
- [x] 3.4 Wrap the Set Area `ion-button` with `@if (isOwner())` so it is only rendered for the owner

## 4. Verification

- [ ] 4.1 Run `ng serve` and verify: typing fewer than 3 chars on the Public tab shows the hint; typing 3+ chars fires a search and renders results
- [ ] 4.2 Verify tapping a public playfield navigates to its detail page showing the map and name (no edit controls)
- [ ] 4.3 Open the detail page from the private list as owner — verify the visibility toggle and Set Area button are visible
- [ ] 4.4 Open the detail page for a public playfield as a non-owner — verify the visibility toggle and Set Area button are absent
