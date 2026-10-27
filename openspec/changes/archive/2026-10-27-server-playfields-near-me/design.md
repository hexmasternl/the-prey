# Design

## Context

See proposal.md for the motivation. The relevant current state:

- **Server.** `SearchPublicPlayFields` is a query slice in the PlayFields module. The endpoint (`PlayFieldEndpoints.SearchPublicPlayFields`) and the handler both reject a `q` shorter than 3 trimmed characters. `TableStoragePlayFieldRepository.SearchPublicAsync(searchText)` queries every entity with `IsPublic` and matches the name in memory; it throws on a blank search text. The domain `PlayField.CenterCoordinates` is the polygon centroid (computed on create/update and stored as `CenterLatitude`/`CenterLongitude`), and it is nullable for old rows. The PlayFields module has no distance function; Games has one in its own `GpsCoordinate`, which PlayFields cannot reference (separate modules).
- **App.** `PublicPlayFieldSearch` owns the debounced name search (366 ms, 3 characters) and is shared by `PlayFieldsViewModel` and `PlayFieldSelectViewModel`; it exposes `query` and a sealed `SearchState`. `LocationSource.positions()` (fused provider, balanced power, last known fix first) already serves the main menu readout and the area editor in the foreground. `readLocationAccess` maps grants to `Missing`, `ApproximateOnly`, `ForegroundOnly` and `Granted`. A haversine `distanceMeters` and a `formatDistance` exist in `ui/game/hud/common/`.
- **Privacy.** The disclosure, the privacy page and the Data safety notes all say location is only collected during an active game. A near-me search sends a position outside a game, so they must be extended in the same change.

## Goals / Non-Goals

**Goals:**
- One endpoint for name search, near-me search and both, backward compatible with installed apps.
- The precise device position never leaves the device; the server never stores, logs or tags coordinates.
- One near-me implementation shared by the playfields list and the selection screen.

**Non-Goals:**
- A geo index in Table Storage (geohash partition/row keys). The in-memory scan stays; it is noted in code for later.
- Distance to the polygon edge, or "you are inside this playfield" detection.
- A player-adjustable radius in the app.
- Requesting location permission from the near-me option, or any background location.

## Decisions

### 1. Default radius 25 km, not adjustable in the app

The server takes an optional `radiusKm`, defaults to 25 and clamps to 1–100 (out-of-range values are clamped, not rejected, so a future client can ask for more without a 400). The app always sends no radius and gets 25 km. 25 km covers a town and its surroundings, which is how far a group plausibly travels to play; a radius control adds UI and strings for little gain at the current number of public playfields. If players ask for it, a radius picker is a pure app change because the server already accepts the parameter.

*Alternative:* a fixed radius without a parameter. Rejected: adding the parameter later would be a second contract change.

### 2. Distance to the centre, not to the nearest edge

Distance is the haversine (great-circle) distance from the given point to `CenterCoordinates`, in whole metres. Playfields without a centre are left out of results that use coordinates. The centre is already stored on every summary, so the filter works on the data the repository loads today without deserialising every polygon. Edge distance would need the full point list per playfield and a point-to-segment computation, and an "inside" check with a position rounded to about 1 km (decision 5) would be wrong near the boundary anyway. Playfields are small compared with the 25 km radius, so the ordering difference is negligible.

*Alternative:* edge distance with distance 0 when inside. Rejected for the reasons above; can be revisited with a geo index.

### 3. Near-me is an explicit option, not the default view

The Public tab keeps opening with the focused search field and the name-search hint. A "Near me" filter chip next to the search field turns near-me on. Sending a position outside a game must be something the player chose, both for the disclosure ("when you ask for it") and so that opening the tab never triggers a location request. The chip's state survives a rotation (saved state) but not leaving the screen, matching how the tab and search text behave.

*Alternative:* near-me as the default when the search box is empty and permission is granted. Rejected: it sends a position every time the tab is opened, without the player asking, and conflicts with the focused-search-field requirement.

### 4. One endpoint, optional parameters, additive response field

`GET /playfields/public?q=&lat=&lon=&radiusKm=`:

| Input | Result |
|---|---|
| `q` only (≥ 3 trimmed) | Today's behaviour: name matches, unordered as today, no cap, `distanceMeters: null`. |
| `lat` + `lon` | Public playfields with a centre within the radius, nearest first, max 50, `distanceMeters` filled. |
| `q` + `lat` + `lon` | Name matches within the radius, nearest first, max 50, `distanceMeters` filled. |
| Neither, only one of `lat`/`lon`, `lat` outside −90..90, `lon` outside −180..180, non-blank `q` shorter than 3 | 400 validation problem keyed on the offending parameter. |

A blank `q` with coordinates is treated as absent. `PlayFieldSummaryDto` gets `int? DistanceMeters = null` as its last positional parameter, so existing constructors and `GET /playfields` are unaffected; older apps ignore the field (`ignoreUnknownKeys = true`). No new problem `code` values.

The query record becomes `SearchPublicPlayFieldsQuery(string? SearchText, GpsCoordinate? Origin, double RadiusKm)`, and the handler validates it as well (defence in depth, as today). The repository method takes a nullable search text (`SearchPublicAsync(string? searchText, ct)`), keeping the name match in the adapter and the distance logic in the handler, where it is unit-testable with a mocked repository. A small static `GeoDistance.Meters(a, b)` helper lives in the PlayFields module's domain models.

*Alternative:* a separate `GET /playfields/public/nearby` endpoint. Rejected: combined name + distance search would need either duplication or a third route.

### 5. Coordinates rounded on the device, distance shown from the device

The app rounds the fix to 2 decimals (about 1.1 km north–south, about 0.7 km east–west in the Netherlands) before sending it. That is coarse enough that the request does not reveal where the player stands, and fine enough for a 25 km radius. The app re-sorts the results and shows a distance computed on the device from its own unrounded fix and each result's centre (with the existing haversine and `formatDistance`), so the label is accurate even though the server worked with the rounded point. The server's `distanceMeters` decides which playfields are in range and is used by the app only when it has no own fix to compare with (for example a restored result list after a configuration change).

Approximate location (coarse permission) is enough. The app takes the first value of `LocationSource.positions()` with a 10 second timeout, only while the Public tab is visible.

### 6. No permission prompt from near-me

Location permission is only requested after the disclosure (`location-tracking-consent`). Near-me does not open the disclosure or a system prompt: with `LocationAccess.Missing` the chip shows a message that near-me needs location access, granted in Settings, and the tab stays in name search. With permission but no fix within the timeout, it shows that the location could not be determined and stays in name search. Background location is never requested for this.

### 7. Telemetry and logging

The handler's activity gets `playfield.search.mode` = `name` | `near` | `both`, `playfield.search.result_count`, and `playfield.search.radius_km` (the clamped integer, low cardinality). `IPlayFieldMetrics.RecordPublicPlayFieldSearch(string mode)` adds the mode as a tag on the existing counter. Coordinates, the search text and user ids are never logged or tagged; the request is not persisted.

### 8. Privacy texts

One new disclosure statement (`location_consent_point_near_me`): when the player uses Near me on the public playfields, the app sends their approximate position once to find nearby playfields; it is not stored. The disclosure body changes from "collects your device's precise location, but only while you are in an active game" to "tracks your device's precise location only while you are in an active game" (Dutch to match), so the two statements do not contradict. The privacy page's location section and table and the Data safety short explanation get the same sentence in English and Dutch. The Data safety answers themselves do not change (location is already collected, for app functionality, not shared).

## Risks / Trade-offs

- [In-memory scan of all public playfields per search] → Fine at current volume; a code comment points to a geohash key or search index when public playfields grow. The 50-result cap bounds the response, not the scan.
- [Rounded origin shifts the radius edge by up to ~0.8 km] → Acceptable for a 25 km discovery radius; the app's own distance labels and order are exact.
- [Old rows without a centre are invisible to near-me] → Every create/update computes a centre; rows that predate it are found by name and get a centre on their next edit.
- [Play review reads the disclosure change as new collection] → The data type and purpose are unchanged; the explanation text names the near-me use, and the use is user-initiated and foreground-only.

## Migration Plan

Server first: the endpoint change is additive, so it can deploy before the app. The app release follows; older apps keep calling `?q=` only. Rollback of the server is safe as long as the app still treats the near-me request failing as a search error (it does: any non-2xx is `SearchState.Error`).
