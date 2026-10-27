# Proposal

> GitHub issue: [#53](https://github.com/hexmasternl/the-prey/issues/53)

## Why

Public playfields can only be found by typing at least three characters of their name, so a player who does not know what is around them cannot discover playfields to play on. Every playfield already has a centre coordinate, so the server can find the ones nearby. Reported in #53.

## What Changes

- `GET /playfields/public` accepts optional `lat`, `lon` and `radiusKm` query parameters. With coordinates, `q` becomes optional; the result holds public playfields whose centre lies within the radius (default 25 km, clamped to 1–100 km), nearest first, at most 50.
- `PlayFieldSummaryDto` gains an optional `distanceMeters` field, filled only for searches with coordinates. Name-only searches and `GET /playfields` are unchanged (the field is `null`), so older apps keep working.
- Validation: `lat` and `lon` come together and must be in range; `q`, when given, still needs 3 trimmed characters; a call with neither `q` nor coordinates is still a 400.
- The search's telemetry tags the search mode (`name`, `near`, `both`); coordinates are never logged, tagged or stored.
- Android app: a "Near me" option on the Public tab of the playfields list and of the playfield selection screen. It uses one foreground location fix (approximate location is enough, no new permission prompt), sends it rounded to about 1 km, shows a distance per playfield and combines with name search. Without location permission or a fix it explains why and falls back to name search.
- The location disclosure, the privacy page and the Play Data safety notes state that the near-me search sends an approximate position once, which is not stored.
- `docs/api/endpoints.md` documents the public search (it is missing today) and the website playfield pages describe near-me, in English and Dutch.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `playfields`: public play-field search can filter and order by distance from a given point, with a distance per result.
- `app-playfields-list`: the Public tab gets a "Near me" option that lists nearby public playfields without typing and shows distances; the hint and three-character rules apply only to name search.
- `playfield-selection`: the same "Near me" option on the selection screen's Public tab.
- `location-tracking-consent`: the disclosure also states that the near-me search sends an approximate position, once, when the player asks for it.

## Impact

- **Server (PlayFields):** `Features/SearchPublicPlayFields` (query, handler, new distance helper), `PlayFieldEndpoints.SearchPublicPlayFields`, `IPlayFieldRepository.SearchPublicAsync` and `TableStoragePlayFieldRepository`, `PlayFieldSummaryDto`, `IPlayFieldMetrics`, unit tests in `HexMaster.ThePrey.PlayFields.Tests/SearchPublicPlayFields/`.
- **API contract:** additive only (new optional query parameters, new nullable response field). No new problem codes.
- **Android app:** `PublicPlayFieldSearch`, `PlayFieldsViewModel`, `PlayFieldSelectViewModel`, their screens, `PlayFieldApi` / `PlayFieldRepository` / `PlayFieldSummaryDto`, `PlayFieldItem`, `AppContainer` wiring of `LocationSource`, strings in `values` and `values-nl`.
- **Privacy / Play:** `location_consent_*` strings, `website/src/content/{en,nl}/privacy.md`, `docs/play-store/location-disclosure.md` (Data safety explanation). No new Android permission.
- **Docs / website:** `docs/api/endpoints.md`, `website/src/content/{en,nl}/playfield/` and the relevant how-to pages.
- **Infra:** none.
