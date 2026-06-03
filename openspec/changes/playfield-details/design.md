## Context

The `list-playfields` change establishes `IPlayfieldService`, `PlayfieldCacheService`, and the `PlayfieldsPage`. The `playfield-details` change adds the create/edit form. A single `PlayfieldDetailsPage` handles both modes — create (no id passed) and edit (id passed via Shell query parameter). The area-editor (coordinate drawing) is a **future change**; this change only wires the "Set Area" button and defines the round-trip contract.

The app currently has no map control and no geolocation usage. Both must be introduced here.

## Goals / Non-Goals

**Goals:**
- Single page for both create and edit, distinguished by `?id=` query parameter
- Name field with live validation (≥ 5 chars)
- Public/Private toggle
- Mini-map preview: polygon shape when coordinates exist, user location centered when empty
- "Set Area" button that navigates to the (future) area editor and receives updated coordinates back
- Save button enabled only when name ≥ 5 chars AND ≥ 3 coordinates; saves locally + to server (POST or PUT)

**Non-Goals:**
- The area editor page itself (a future change owns that)
- Offline save without eventual server sync (if the device is offline, save locally and retry is out of scope; an error is surfaced instead)
- Playfield image / thumbnail upload
- Multi-user collaboration or locking

## Decisions

### 1. Dual-mode page via Shell query parameter

`PlayfieldDetailsPage` is registered as `"playfield-details"`. The `list-playfields` change passes `?id={id}` for edit and no id for create. A `[QueryProperty("PlayfieldId", "id")]` attribute drives the mode.

**Rationale:** Avoids two nearly-identical pages and keeps navigation simple. The page loads the playfield from cache when `PlayfieldId` is set; otherwise it initialises a blank model.

### 2. Map control: `Microsoft.Maui.Controls.Maps`

Use the built-in `Microsoft.Maui.Controls.Maps` package for the mini-map. A `Polygon` overlay renders the coordinate shape; a `Pin` marks the user's location when no coordinates exist.

**Alternatives considered:**
- `GraphicsView` custom drawing — no API key required, but cannot show real tiles, making the map useless for spatial orientation.
- WebView + Leaflet — full control, but adds JS/HTML complexity and a bundled asset to maintain.

**Trade-off:** Platform API keys are required (Google Maps on Android, Apple Maps on iOS; Bing Maps on Windows). These must be configured in `MauiProgram.cs` and platform manifests. The design accepts this complexity as unavoidable for a map-centric game.

### 3. Coordinate round-trip via `PlayfieldEditingContext` singleton

The area editor (future change) needs to receive the current coordinates and return updated ones. Passing a list of `Location` objects via Shell query parameters is not safe (string serialisation, URL length limits).

A singleton `PlayfieldEditingContext` service holds `CurrentCoordinates` (a `List<Location>`). Before navigating to the area editor, `PlayfieldDetailsPage` writes the current coordinates to this context. On return (`OnAppearing`), the page reads back the (possibly updated) coordinates.

**Alternatives considered:**
- `WeakReferenceMessenger` (CommunityToolkit.Mvvm) — clean but adds a dependency not yet in the project.
- Shell navigation result / `NavigationResult` — not yet a first-class MAUI primitive for arbitrary objects.

**Rationale:** Simplest zero-dependency approach. The singleton is owned by the same DI container already used for `IAuthService`.

### 4. Save strategy: online-only with local cache write

On save:
1. Write to local cache (upsert).
2. POST (create) or PUT (edit) to server.
3. If server call fails, show error alert — the local cache write remains so the user does not lose their input on retry.

**Alternatives considered:**
- Queue for background sync — needed for true offline edit support; out of scope for this change.

### 5. Geolocation: one-shot current location for empty map center

When the page opens in create mode with no coordinates, call `Geolocation.GetLastKnownLocationAsync()` (fast, cached) and fall back to `Geolocation.GetLocationAsync()` (fresh fix, slower) if nothing is cached. Center the map there.

**Trade-off:** If the user denies location permission, the map defaults to a fixed fallback coordinate (e.g., 0,0 or a configurable default). An alert explains why the map is not centered.

## Risks / Trade-offs

- **API key management**: Google Maps and Bing Maps keys must not be committed to source control. → Use `appsettings.json` excluded from git, or environment variables injected at build time; document in README.
- **Map polygon on Windows**: MAUI Maps `Polygon` support on Windows (via Bing Maps) has historically been incomplete. → Test early; fall back to pin-only preview on Windows if needed, with a warning label.
- **Geolocation on iOS simulator**: Simulator does not always provide a real location fix. → Use `GetLastKnownLocationAsync` first; document simulator limitation.
- **Coordinates lost on back-navigation before save**: If the user edits coordinates in the area editor then navigates back without saving, the `PlayfieldEditingContext` still holds updated coordinates. → The details page must apply them on `OnAppearing` and prompt the user to save.

## Open Questions

- What coordinate format does the server API expect? (GeoJSON `Polygon`, flat `[{lat, lon}]` array, or other?) → Confirm with backend team before implementing `IPlayfieldService.CreatePlayfieldAsync`.
- Are there server-side limits on the number of coordinates per playfield?
