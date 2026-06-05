## 1. Page scaffold

- [ ] 1.1 Create `src/app/playfields/playfield-selection/playfield-selection.page.ts` as a standalone Ionic modal page component
- [ ] 1.2 Create `playfield-selection.page.html` with header (title + cancel button), searchbar, list, and footer toolbar with "Select" button
- [ ] 1.3 Create `playfield-selection.page.scss` for selected-row highlight and badge styles

## 2. Local list display

- [ ] 2.1 On modal open (`ionViewWillEnter`), load all records from `PlayfieldsDbService.getAll()` and store in a signal
- [ ] 2.2 Render each row with name, public/private badge (`isPublic`), and mine/external badge (compare `record.ownerId` to `UserStateService.profile()?.userId`)
- [ ] 2.3 Show empty-state message when the list signal is empty and no search is active

## 3. Server search

- [ ] 3.1 Wire a `Subject<string>` search pipeline with `debounceTime(400)`, `distinctUntilChanged()`, `filter(v => v.length >= 3)`, `switchMap` to `PlayfieldsService.searchPublicPlayfields()`
- [ ] 3.2 Show server results in place of the local list when search is active (3+ chars); restore local list when query drops below 3 chars
- [ ] 3.3 Show a loading spinner while the server request is in flight
- [ ] 3.4 Silently fall back to the local list on search error (no error toast needed)

## 4. Row selection

- [ ] 4.1 Track selected playfield in a signal (`selectedPlayfield = signal<PlayFieldRecord | null>(null)`)
- [ ] 4.2 Tapping an unselected row selects it; tapping the selected row deselects it
- [ ] 4.3 Apply a visual highlight class to the selected row

## 5. Confirm and cancel

- [ ] 5.1 Disable the "Select" button when `selectedPlayfield()` is `null`; enable when non-null
- [ ] 5.2 On "Select" press, call `modalController.dismiss({ playfield: selectedPlayfield() })`
- [ ] 5.3 On cancel button press (or back gesture), call `modalController.dismiss(null)`

## 6. i18n

- [ ] 6.1 Add translation keys for the page: title, search placeholder, empty state, "Mine"/"External" badges, "Public"/"Private" badges, cancel and select button labels
- [ ] 6.2 Add English (`en.json`) and Dutch (`nl.json`) translations
