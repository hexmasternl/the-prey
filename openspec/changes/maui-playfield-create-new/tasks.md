## 1. Mapsui dependency & bootstrap

- [ ] 1.1 Add the `Mapsui.Maui` NuGet package to `HexMaster.ThePrey.Maui.App.csproj`
- [ ] 1.2 Initialise Mapsui in `MauiProgram` (Mapsui MAUI registration/`UseMauiMaps`-equivalent) so the `MapControl` renders on Android/iOS/Windows
- [ ] 1.3 Confirm the project still builds for Android (`dotnet build ... -f net10.0-android`) after adding the dependency

## 2. Client model & create API method

- [ ] 2.1 Add a client model for a create point (`GpsCoordinate(double Latitude, double Longitude)`) in `Services/Api/` (or reuse the existing coordinate model if `maui-playfields-list-page` introduced one)
- [ ] 2.2 Add `CreatePlayFieldResult` union to `Services/Api/`: `Success(PlayFieldSummary)` / `Validation` / `Unauthorized` / `Error`
- [ ] 2.3 Add `CreatePlayFieldAsync(string name, bool isPublic, IReadOnlyList<GpsCoordinate> points, string accessToken, CancellationToken)` to `IPlayFieldApiClient`
- [ ] 2.4 Implement `CreatePlayFieldAsync` in `PlayFieldApiClient`: `POST /playfields` with `CreatePlayFieldRequest`-shaped JSON body and `Authorization: Bearer`; map `201`→Success (deserialize `PlayFieldDto`, project to `PlayFieldSummary(Id, Name, IsPublic)`), `400`→Validation, `401`→Unauthorized, network/timeout/unexpected→Error (catch `HttpRequestException`/`TaskCanceledException`), mirroring `GameApiClient`

## 3. Name-pattern validator

- [ ] 3.1 Add a pure `PlayfieldNameValidator.IsPublishable(string? name)` returning true only when the trimmed name splits on `,` into exactly three non-empty parts and part 1 matches `^[A-Z]{2,3}$`
- [ ] 3.2 Unit-test the validator: valid `NL, Amsterdam, City park` and `FRA, Paris, The Mall`→true; `Amsterdam park`, `Nl, Amsterdam, Park`, `USAA, X, Y`, empty, two-part, and blank-part inputs→false

## 4. Area editor view model

- [ ] 4.1 Add `DefineAreaViewModel : ObservableObject` holding an ordered vertex collection of `GpsCoordinate`, the selected vertex index (nullable), `CanSave` (≥ 3 vertices), and `MaxPoints = 100`
- [ ] 4.2 Implement `AddVertex(lat, lon)` (no-op at 100 points), `SelectVertex(index)` (single selection), `MoveSelected(lat, lon)`, `DeleteSelected()` (clears selection), and a way to seed the collection from an incoming polygon
- [ ] 4.3 Expose Save (returns ordered points when `CanSave`) and Cancel (returns nothing) commands/results
- [ ] 4.4 Unit-test the rules: add up to and beyond 100; select/reselect single; move updates coordinate; delete removes + clears selection; `CanSave` flips at 3 points; seed pre-populates

## 5. Area editor page (Mapsui)

- [ ] 5.1 Add `Pages/DefineAreaPage.xaml` (+ `.xaml.cs`) hosting a Mapsui `MapControl`, plus Cancel, Save (bound to `CanSave`), and a Trash action (visible only when a vertex is selected)
- [ ] 5.2 Centre the map on the current location via `IGpsReader`; on `null` use a sensible default centre/zoom without crashing
- [ ] 5.3 Render vertices as green dot features and, at ≥ 3 vertices, a green ~25%-opacity filled polygon with green outline; redraw whenever the vertex set changes
- [ ] 5.4 Wire gestures: single tap on empty map → `AddVertex`; tap on an existing vertex (within a hit radius) → `SelectVertex` and show it with a red outline; drag of a selected vertex → `MoveSelected`; empty-area drag/pinch remain pan/zoom
- [ ] 5.5 Trash → `DeleteSelected`; Save → return the ordered polygon to the create page and close; Cancel → close returning nothing (per design D3 result hand-off)

## 6. Create playfield view model

- [ ] 6.1 Add `CreatePlayfieldViewModel : ObservableObject` with `Name`, `IsPublic`, `CanTogglePublic` (from `PlayfieldNameValidator`), a held polygon, `HasArea` (≥ 3 points), `CanSave` (name non-empty AND `HasArea`), and busy/error state
- [ ] 6.2 In the `Name` setter recompute `CanTogglePublic`; when it becomes false, force `IsPublic = false`
- [ ] 6.3 Add a Define-Area command that opens `DefineAreaPage` (passing the held polygon) and stores the returned polygon; leave the polygon unchanged on cancel
- [ ] 6.4 Add a Save command (guarded by `CanSave`): acquire an access token via `IAccessTokenProvider`; if none → error state; else call `CreatePlayFieldAsync` and map Success→return the created `PlayFieldSummary` and close, Validation→validation error (keep name+area), Unauthorized→invalidate token + error, Error→error state; toggle busy around the call
- [ ] 6.5 Add a Cancel command that closes without sending a request
- [ ] 6.6 Unit-test the VM: toggle gating + reset-to-Private; `CanSave` combinations; Save maps each result correctly (Moq `IPlayFieldApiClient`/`IAccessTokenProvider`); no token → error; Unauthorized invalidates token; Cancel sends nothing

## 7. Create playfield page & list integration

- [ ] 7.1 Add `Pages/CreatePlayfieldPage.xaml` (+ `.xaml.cs`): name entry, Public/Private toggle bound to `IsPublic`/`CanTogglePublic`, Define Area button, an "area defined" indicator bound to `HasArea`, Save (bound to `CanSave`) and Cancel; a busy/error region — no inline visual literals
- [ ] 7.2 Add the `+` action to the Private tab in `Pages/PlayfieldsPage.xaml`(+ `.xaml.cs`) that navigates to `CreatePlayfieldPage`; ensure it is present even in the empty state
- [ ] 7.3 On returning from a successful create, append the returned `PlayFieldSummary` to the Private collection (fallback: reload the Private list if the returned summary is unusable, per design)
- [ ] 7.4 Register `CreatePlayfieldViewModel`, `DefineAreaViewModel`, `CreatePlayfieldPage`, and `DefineAreaPage` in `MauiProgram`; register the create page's Shell route

## 8. Theme resources

- [ ] 8.1 Add styles to `Resources/Styles/Styles.xaml` for the create form (name field, toggle row, "area defined" indicator) reusing existing `Tp*` color tokens — no inline literals
- [ ] 8.2 Add styles for the Define Area / Save / Cancel buttons and the map action buttons (Save, Cancel, Trash)
- [ ] 8.3 Define green vertex, red-selected-vertex, and green transparent polygon appearance as named resources/constants consumed by the Mapsui rendering (single source of truth for those colors/opacity)

## 9. Verification

- [ ] 9.1 Build for Android (`dotnet build src/HexMaster.ThePrey.Maui.App/HexMaster.ThePrey.Maui.App.csproj -f net10.0-android`) with 0 warnings / 0 errors; run the MAUI unit tests and confirm all pass
- [ ] 9.2 Review `CreatePlayfieldPage.xaml`, `DefineAreaPage.xaml`, and the Private-tab `+` markup for no inline color/opacity/size/border/glow literals (single-source-of-truth styling rule); only layout properties inline
- [ ] 9.3 On device/emulator: from an empty Private tab, tap `+`; enter `NL, Amsterdam, City park` and confirm the toggle enables (and reverts to Private when the name is broken); Define Area centres on current location, taps add green dots, ≥ 3 draw a green transparent polygon, a tapped dot gets a red border and can be dragged and trashed, up to 100 points; Save (enabled at ≥ 3) returns the area; Save on the create page creates the playfield and it appears in the Private list; Cancel paths discard cleanly; a signed-out/expired session shows an error without crashing
