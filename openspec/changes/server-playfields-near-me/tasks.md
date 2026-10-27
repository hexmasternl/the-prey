# Tasks

## 1. Server: near-me public search (PlayFields)

- [x] 1.1 Add a `GeoDistance.Meters(GpsCoordinate a, GpsCoordinate b)` haversine helper to the PlayFields domain models, with unit tests for a known pair (Amsterdam–Utrecht ≈ 35 km) and zero distance; verify with `dotnet test server/src/PlayFields/HexMaster.ThePrey.PlayFields.Tests/`
- [x] 1.2 Add `int? DistanceMeters = null` as the last parameter of `PlayFieldSummaryDto` and a `ToSummaryDto(int? distanceMeters)` overload in `PlayFieldMappings`; verify the solution builds and existing tests still pass (`GET /playfields` output unchanged)
- [x] 1.3 Change `IPlayFieldRepository.SearchPublicAsync` to take a nullable search text and update `TableStoragePlayFieldRepository` (no name filter when null; code comment on the in-memory scan and a future geohash/search index); verify with a build and the existing repository tests, if any
- [x] 1.4 Extend `SearchPublicPlayFieldsQuery` with `GpsCoordinate? Origin` and `RadiusKm` (constants: default 25, min 1, max 100, max results 50) and the handler: validation, radius clamp, centre-distance filter, nearest-first order, cap, `DistanceMeters`; verify with new tests in `Tests/SearchPublicPlayFields/` for name-only (unchanged, null distance), near-only, both, missing centre excluded, radius boundary, clamp, cap of 50 and every validation error (Bogus fakers in `Tests/Factories/`)
- [x] 1.5 Instrument the handler: `playfield.search.mode` (`name`/`near`/`both`), `result_count`, clamped `radius_km`; change `IPlayFieldMetrics.RecordPublicPlayFieldSearch` to take the mode as a tag; no coordinates or search text in logs or tags; verify with a test on the metrics mock and by reading the log statements
- [x] 1.6 Extend the `SearchPublicPlayFields` endpoint with optional `lat`, `lon`, `radiusKm` and the 400 rules from the `playfields` delta spec (lat/lon pairing and ranges, `q` length, neither given, blank `q` with a position ignored); verify with endpoint-level tests or a local call through the Aspire gateway (`the-prey-local-dev`) for each row of the design's table
- [x] 1.7 Document `GET /playfields/public` (parameters, defaults, ordering, cap, `distanceMeters`, 400 cases, backward compatibility) in `docs/api/endpoints.md`; verify the documented examples match the endpoint's responses

## 2. Android app: Near me

- [x] 2.1 Add `lat`, `lon` (nullable) to `PlayFieldApi.searchPublic`, `q` nullable, `distanceMeters: Int? = null` to `PlayFieldSummaryDto.kt`, and `searchPublic(query, origin)` to `PlayFieldRepository` with the position rounded to 2 decimals; verify with unit tests on the rounding and the DTO parsing with and without `distanceMeters`
- [x] 2.2 Extend `PublicPlayFieldSearch` with a near-me state (off, locating, on with fix, unavailable: no permission / no fix) driven by an injected `LocationSource` and location-access reader: one fix with a 10 s timeout, combined with the debounced text, results re-sorted and distances computed on the device; verify with JUnit + coroutines-test using fake location and repository for every scenario in the `app-playfields-list` delta spec
- [x] 2.3 Wire the near-me state through `PlayFieldsViewModel` and `PlayFieldSelectViewModel` (saved state for the chip, `LocationSource` from `AppContainer` via `ThePreyNavHost`), keeping the selection when Near me changes; verify with view model tests for rotation restore and selection kept
- [ ] 2.4 Add the "Near me" filter chip, the distance label on `PlayFieldItem` rows (existing `formatDistance`), the updated hint, the no-permission, no-fix and nothing-nearby messages to both Public tabs, using theme tokens only; verify on an emulator with a mock location and with location permission off — **auto: needs human:** UI is in with previews; the emulator check with a mock location and with permission off needs a human
- [x] 2.5 Add all new strings in `res/values/strings.xml` and `res/values-nl/strings.xml` (`the-prey-multilingual`); verify `./gradlew lintDebug testDebugUnitTest` from `app/src` passes with no missing translations

## 3. Privacy and Play Store

- [ ] 3.1 Change `location_consent_body` to the "tracks … only while you are in an active game" wording and add `location_consent_point_near_me` in English and Dutch, shown in the disclosure dialog and the Settings panel; verify both languages on a device and update any disclosure UI tests — **auto: needs human:** strings and the extra disclosure point are in (no disclosure UI tests exist); the on-device check in both languages needs a human
- [x] 3.2 Update `docs/play-store/location-disclosure.md` (disclosure table, permission declaration text, Data safety short explanation EN + NL) to match; verify the strings in the doc equal the resource values
- [ ] 3.3 Add the near-me use to `website/src/content/en/privacy.md` and `website/src/content/nl/privacy.md` (data table and Location Data section: approximate position, once, on request, not stored); verify with `hugo server` that both pages render

## 4. Website and docs

- [ ] 4.1 Describe Near me on `website/src/content/{en,nl}/playfield/_index.md` and in the how-to for creating a game (`how-to/create-a-game.md`, EN + NL), following `the-prey-docs-sync` and `the-prey-website-content`; verify with `hugo server` in both languages

## 5. Integration check

- [ ] 5.1 Run the system locally (Aspire + emulator with a mock location near a public playfield) and confirm: near-me lists it with a distance, combined name search works, a build of the previous app version still searches by name, and the PlayFields logs and traces contain no coordinates
