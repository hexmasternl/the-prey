## 1. Data Model & IndexedDB Store

- [x] 1.1 Create `src/app/playfields/playfield.model.ts` with `PlayfieldDto`, `PlayfieldRecord` (adds `isSynced: boolean`) and `PlayfieldVisibility` enum
- [x] 1.2 Inspect `user-db.service.ts` to determine if it exposes a shared IDB instance or opens the DB independently
- [x] 1.3 Create `src/app/playfields/playfields-db.service.ts` — bump IDB version, add `playfields` object store, implement `upsert`, `getAll`, `getUnsynced`, `markSynced`, `delete` methods per spec
- [x] 1.4 Verify IndexedDB schema migration: confirm `UserDbService` still works after the version bump

## 2. API Service

- [x] 2.1 Create `src/app/playfields/playfields.service.ts` with `getMyPlayfields(): Observable<PlayfieldDto[]>` calling `GET /playfields`
- [x] 2.2 Add `postPlayfield(record: PlayfieldRecord): Observable<void>` calling `POST /playfields`
- [x] 2.3 Add `syncPlayfields()` method implementing the two-phase sync: download → merge into IndexedDB (server-wins on `lastUpdatedOn`), then upload unsynced records

## 3. Page Component

- [x] 3.1 Create `src/app/playfields/playfields-list.page.ts` as a standalone Ionic/Angular component with `ion-segment` tab state signal (`'private' | 'public'`)
- [x] 3.2 Create `src/app/playfields/playfields-list.page.html` — header, `ion-segment` with "Private"/"Public" buttons, `@switch` block rendering private list or public placeholder
- [x] 3.3 Create `src/app/playfields/playfields-list.page.scss` with any page-specific styles
- [x] 3.4 Implement private list as `ion-list` of `ion-item-sliding` — label with playfield name, `ion-badge` tag showing "Private"/"Public", swipe-left delete `ion-item-option` (red)
- [x] 3.5 Wire tap handler on `ion-item` to navigate to `/playfields/:id`
- [x] 3.6 Wire delete handler — call `PlayfieldsDbService.delete(id)`, remove item from local signal list
- [x] 3.7 Add public tab placeholder with "Coming soon" message
- [x] 3.8 Show loading spinner while sync is in progress; show error state with retry button on failure

## 4. Routing

- [x] 4.1 Update `app.routes.ts`: change `/playfields` route to lazy-load `PlayfieldsListPage` instead of `HomePage`
- [x] 4.2 Add a `/playfields/:id` route stub (component can be a placeholder) so navigation from the list doesn't 404

## 5. Integration & Verification

- [x] 5.1 Run `ng serve` and verify navigating from home → playfields works end-to-end
- [ ] 5.2 Verify sync: mock or use dev server, confirm server-newer records update local, older records are skipped
- [ ] 5.3 Verify unsynced records are posted on load
- [ ] 5.4 Verify swipe-left delete removes item from list and IndexedDB
- [ ] 5.5 Verify badge tag renders correctly for private vs public playfields
- [ ] 5.6 Verify auth guard blocks unauthenticated access to `/playfields`
