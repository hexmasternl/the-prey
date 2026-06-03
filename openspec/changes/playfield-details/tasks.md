## 1. Dependencies & Infrastructure

- [x] 1.1 Add `Microsoft.Maui.Controls.Maps` NuGet package to `ThePrey.Application.App.csproj`
- [x] 1.2 Configure map platform keys: add `UseMauiMaps()` in `MauiProgram.cs` and set Google Maps API key in `AndroidManifest.xml` and Bing Maps key for Windows (document key sources in README; do NOT commit keys to source control)
- [x] 1.3 Extend `Playfield` model with `Coordinates` (`List<Location>`) and `IsPublic` (`bool`) properties if not already present
- [x] 1.4 Add `CreatePlayfieldAsync(Playfield)` and `UpdatePlayfieldAsync(Playfield)` to `IPlayfieldService` and implement in `PlayfieldService` (POST and PUT respectively)
- [x] 1.5 Add upsert support to `PlayfieldCacheService` (`UpsertPlayfieldAsync(Playfield)`)
- [x] 1.6 Create `Services/PlayfieldEditingContext.cs` singleton with `CurrentCoordinates` (`List<Location>`) property for passing coordinate state to/from the area editor

## 2. Playfield Details Page — Shell & Routing

- [x] 2.1 Create `PlayfieldDetailsPage.xaml` and `PlayfieldDetailsPage.xaml.cs` in the App project
- [x] 2.2 Register `"playfield-details"` route in `AppShell.xaml.cs`
- [x] 2.3 Add `[QueryProperty("PlayfieldId", "id")]` to `PlayfieldDetailsPage` to support both create (no id) and edit (with id) modes
- [x] 2.4 Register `PlayfieldEditingContext` as a singleton in `MauiProgram.cs`

## 3. Form UI

- [x] 3.1 Add `Entry` for playfield name with placeholder text and inline validation label (shown when field loses focus with < 5 chars)
- [x] 3.2 Add `Switch` (or `CheckBox`) for Public/Private visibility with a descriptive label; default to private (off)
- [x] 3.3 Add `Map` control (from `Microsoft.Maui.Controls.Maps`) as the mini-map preview, sized appropriately (e.g., 200dp tall)
- [x] 3.4 Add "Set Area" `Button` below the mini-map
- [x] 3.5 Add "Save" `Button` (or toolbar item) with `IsEnabled` bound to form validity

## 4. Page Logic — Create & Edit Modes

- [x] 4.1 In `OnAppearing`, check if `PlayfieldId` is set; if yes, load from cache and populate form fields, set page title to "Edit Playfield"; if no, clear form and set title to "New Playfield"
- [x] 4.2 Handle playfield-not-found in cache: show error alert and navigate back to the playfields list
- [x] 4.3 On `OnAppearing`, read `PlayfieldEditingContext.CurrentCoordinates` and refresh the mini-map and re-evaluate Save button state (handles returning from area editor)

## 5. Mini-Map Logic

- [x] 5.1 When coordinates exist: clear map overlays, add a `Polygon` element connecting all coordinates, and call `MoveToRegion` to fit the shape bounds
- [x] 5.2 When no coordinates exist: request current location via `Geolocation.GetLastKnownLocationAsync()` (fall back to `GetLocationAsync()` with a short timeout); center map on result
- [x] 5.3 Handle location permission denied or unavailable: show map at a default position and display a notice label explaining location could not be determined

## 6. Validation

- [x] 6.1 Implement a `IsFormValid` computed property/method: `name.Length >= 5 && coordinates.Count >= 3`
- [x] 6.2 Wire name `Entry.Unfocused` event to trigger validation message visibility
- [x] 6.3 Wire name `Entry.TextChanged` event to re-evaluate `IsFormValid` and update Save button `IsEnabled`
- [x] 6.4 Re-evaluate `IsFormValid` after coordinates are updated from the editing context

## 7. Set Area Navigation

- [x] 7.1 In "Set Area" button handler, write current coordinates to `PlayfieldEditingContext.CurrentCoordinates` and navigate to `"playfield-area-editor"` route (register a placeholder route if the area editor page does not exist yet)

## 8. Save Logic

- [x] 8.1 Build `Playfield` object from form fields (id for edit, new guid for create, name, isPublic, coordinates)
- [x] 8.2 Call `PlayfieldCacheService.UpsertPlayfieldAsync` to persist locally
- [x] 8.3 Call `IPlayfieldService.CreatePlayfieldAsync` (create) or `UpdatePlayfieldAsync` (edit); on success navigate back to `"playfields"`
- [x] 8.4 On server error (non-success): show error alert, remain on page (local cache write retained)
- [x] 8.5 On 401 response: navigate to login route

## 9. Localization

- [x] 9.1 Add string resources for: "New Playfield" (title), "Edit Playfield" (title), "Set Area" (button), "Save" (button), name validation message, location unavailable notice, not-found error message, save error message — in both `AppResources.resx` and `AppResources.nl.resx`

## 10. Verification

- [ ] 10.1 Open "Create new" from playfields list; verify empty form with private default and "New Playfield" title
- [ ] 10.2 Verify name < 5 chars shows validation message and Save is disabled
- [ ] 10.3 Verify Save button enables when name ≥ 5 chars AND ≥ 3 coordinates are set
- [ ] 10.4 Verify mini-map centers on user location when no coordinates exist (on device/emulator with location enabled)
- [ ] 10.5 Tap an existing playfield from the list; verify form pre-populates and title reads "Edit Playfield"
- [ ] 10.6 Tap "Set Area"; verify navigation fires to the area editor (or placeholder)
- [ ] 10.7 Save a new playfield; verify it appears in the playfields list and is persisted locally
- [ ] 10.8 Edit an existing playfield and save; verify changes are reflected in the list
