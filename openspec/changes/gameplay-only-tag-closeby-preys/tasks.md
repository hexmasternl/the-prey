## 1. Domain — proximity rule

- [x] 1.1 Add `TagRangeMeters = 50` constant to `Game` domain constants (`src/Games/HexMaster.ThePrey.Games/DomainModels/Game.cs`).
- [x] 1.2 Add a `TaggablePreysWithin(double rangeMeters)` (or similar) read method to `Game` that returns each `Active`/`Passive` prey whose `LatestKnownLocation` is within range of the hunter's `LatestKnownLocation`, together with the computed distance; returns empty when the hunter has no `LatestKnownLocation`.
- [x] 1.3 Add a proximity guard to `Game.TagParticipant`: after the existing state/auth/head-start checks, throw `InvalidOperationException` when the target prey's `LatestKnownLocation` is missing or more than `TagRangeMeters` from the hunter's `LatestKnownLocation`.

## 2. Abstractions — DTOs

- [x] 2.1 Add `TagCandidateDto(Guid UserId, string Callsign, string State, double DistanceMeters)` to `Games.Abstractions/DataTransferObjects/`.
- [x] 2.2 Add `TagCandidatesDto(double RangeMeters, IReadOnlyList<TagCandidateDto> Candidates)` response DTO.

## 3. Server — query feature slice

- [x] 3.1 Create `Features/GetTagCandidates/` with `GetTagCandidatesQuery(Guid GameId, Guid CallerId)` and `GetTagCandidatesQueryHandler` implementing `IQueryHandler<GetTagCandidatesQuery, TagCandidatesDto?>`.
- [x] 3.2 Handler: load game (null → endpoint 404), verify caller is hunter (else signal 403), compute candidates via `Game.TaggablePreysWithin(Game.TagRangeMeters)`, map to `TagCandidatesDto`.
- [x] 3.3 Instrument the handler with `GameActivitySource` (`GetTagCandidates` activity, low-cardinality tags: `game.id`, candidate count) and error status/exception handling per the OTel pattern.
- [x] 3.4 Register the handler in `GamesModuleRegistration.cs`.

## 4. Server — endpoint

- [x] 4.1 Add `GET /games/{id:guid}/tag-candidates` to `GameEndpoints.cs`, extracting the caller id from the `sub` claim and dispatching the query.
- [x] 4.2 Map results to HTTP: 200 with `TagCandidatesDto`, 403 when caller is not the hunter, 404 when the game is not found. Ensure the route is in the `RequireAuthorization()` group.
- [x] 4.3 Confirm the existing tag endpoint maps the new out-of-range `InvalidOperationException` from `TagParticipant` to HTTP 409 (reuse existing exception→status mapping).

## 5. Server — unit tests

- [x] 5.1 `Tests/GetTagCandidates/` — candidate returned within 50 m; excluded beyond 50 m; boundary at exactly 50 m included.
- [x] 5.2 Tagged/Out preys excluded regardless of distance; uses most recent reading (not stale) for both hunter and prey; prey with no location excluded; hunter with no location → empty list.
- [x] 5.3 Non-hunter caller rejected (403 mapping); unknown game → null/404.
- [x] 5.4 `TagParticipant` tests: out-of-range target throws (→409); hunter with no location throws; in-range Active/Passive still tags successfully.
- [x] 5.5 Reuse/extend `Tests/Factories` fakers to build participants with location histories; ensure ≥80% coverage on new domain/handler code.

## 6. Client — service

- [x] 6.1 Add `TagCandidateDto` / `TagCandidatesDto` interfaces to `games.service.ts`.
- [x] 6.2 Add `getTagCandidates(gameId): Promise<TagCandidatesDto>` calling `GET {apiBase}/{gameId}/tag-candidates`.

## 7. Client — hunter view

- [x] 7.1 Replace the locally computed `taggablePrey` signal with a `tagCandidates` signal populated from the endpoint; add `tagCandidatesLoading`/error signals.
- [x] 7.2 Make `openTagModal()` async: open the drawer, call `getTagCandidates`, populate `tagCandidates`; show loading then results.
- [x] 7.3 Update the drawer template to list `tagCandidates` (optionally show distance), show a "no preys in range" empty state, and a retryable error state. Keep the existing two-step confirm + acknowledge flow.
- [x] 7.4 Handle a 409 from `tagPlayer` (prey left range) by surfacing a message and letting the hunter reopen the drawer; keep the in-flight disable behavior.
- [x] 7.5 Add/adjust i18n keys for the empty-state, loading, and out-of-range messages.

## 8. Verification

- [x] 8.1 `dotnet build src/the-prey.slnx` and `dotnet test src/Games/HexMaster.ThePrey.Games.Tests/` pass.
- [x] 8.2 Client builds/lints clean; manual check: opening the tag drawer lists only in-range preys and tagging an out-of-range prey is rejected.
