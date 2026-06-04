# Tasks — Playfield Select View

## 1. Server — public playfield search slice

- [x] 1.1 Add `SearchPublicAsync(string searchText, CancellationToken ct)` to `IPlayFieldRepository` and implement it in the Table Storage adapter (query public playfields, case-insensitive in-memory name-contains filter)
- [x] 1.2 Add feature slice `Features/SearchPublicPlayFields/` with `SearchPublicPlayFieldsQuery(string SearchText)` (sealed record) and `SearchPublicPlayFieldsQueryHandler` returning `IReadOnlyList<PlayFieldSummaryDto>`; reject search text shorter than 3 characters with `ArgumentException`
- [x] 1.3 Instrument the handler with OTel: activity via `PlayFieldActivitySource`, error status + exception recording on failure, and a search counter on `IPlayFieldMetrics`/`PlayFieldMetrics` (no user ids or raw query text in tags)
- [x] 1.4 Register the handler in `PlayFieldsModuleRegistration`
- [x] 1.5 Map `GET /public` on the authorized `/playfields` group in `PlayFieldEndpoints`: parse `q`, return `ValidationProblem` for missing/too-short query, 200 with summaries otherwise
- [ ] 1.6 Unit tests in `HexMaster.ThePrey.PlayFields.Tests/SearchPublicPlayFields/` (xUnit + Moq + Bogus): matching/public-only/empty results, too-short query rejected, repository mock verification
- [ ] 1.7 Run `dotnet test src/PlayFields/HexMaster.ThePrey.PlayFields.Tests/` and confirm green

## 2. App — selection context and service plumbing

- [ ] 2.1 Add `PlayfieldSelectionContext` (singleton) with `SelectedPlayfield`, `SelectionCompleted`, and `Reset()`; register in `MauiProgram.cs`
- [ ] 2.2 Verify `IPlayfieldService.SearchPublicPlayfieldsAsync` against the new endpoint contract (route, query param, summary payload mapping to `Playfield` model) and adjust mapping if needed

## 3. App — PlayfieldSelectPage

- [ ] 3.1 Create `PlayfieldSelectPage.xaml` + code-behind: search box on top, list below, Select button (disabled by default); register transient in `MauiProgram.cs` and add `PlayfieldSelectRoute` in `AppShell`
- [ ] 3.2 Default state: load cached playfields in `OnAppearing`, render synced ones selectable; call `PlayfieldSelectionContext.Reset()` on open
- [ ] 3.3 Render unsynced playfields (`IsSynchronized == false`) disabled with a not-synchronized hint; tapping them shows the hint and never selects
- [ ] 3.4 Search behavior: <3 chars shows the default local list and cancels in-flight searches; ≥3 chars runs the 400 ms debounced search (`CancellationTokenSource` pattern from `PlayfieldsPage`), cancelled in `OnDisappearing`
- [ ] 3.5 Hybrid results: merge local case-insensitive name matches (owned, from cache) with server public results, de-duplicate by `Id` preferring the local copy; on server failure show local matches plus a non-blocking error hint
- [ ] 3.6 Selection state: tap a selectable row to highlight + enable Select; tapping another row moves the selection; Select stores the playfield in `PlayfieldSelectionContext`, marks `SelectionCompleted`, and navigates back
- [ ] 3.7 Back navigation without Select leaves `SelectionCompleted` false (cancelled pick)

## 4. App — localization

- [ ] 4.1 Add all new strings (page title, search placeholder, min-characters prompt, not-synchronized hint, Select button, empty/error states) to `AppResources.resx` and `AppResources.nl.resx`, expose via `AppLocalizer`

## 5. Verification

- [ ] 5.1 Build the MAUI app for Android (`dotnet build ... -f net10.0-android`) and the PlayFields API; confirm both compile
- [ ] 5.2 Manual check against spec scenarios: offline default list, unsynced row disabled, hybrid search with de-duplication, search cancel on <3 chars, selection hand-back via context, Dutch localization
