## 1. Backend — DTO & feature slice

- [ ] 1.1 Add `CheckAppVersionRequest` record to `HexMaster.ThePrey.Games.Abstractions/DataTransferObjects/` with a `current-version` property (JSON name `current-version`).
- [ ] 1.2 Create `Features/CheckAppVersion/CheckAppVersionQuery.cs` (`sealed record` carrying the parsed/raw client version).
- [ ] 1.3 Create a result type for the query (e.g. `AppVersionCheckResult` enum: `UpToDate`, `UpdateRequired`).
- [ ] 1.4 Create `Features/CheckAppVersion/CheckAppVersionQueryHandler.cs` implementing `IQueryHandler<CheckAppVersionQuery, AppVersionCheckResult>`: read minimum from `IConfiguration` key `Games:MinimumAppVersion`, numeric component-wise compare, return result. Treat absent/empty key as `UpToDate`.
- [ ] 1.5 Add OpenTelemetry: start a `CheckAppVersion` activity via `GameActivitySource.Source`, tag `version.outcome` (`up_to_date`/`update_required`), set error status + add exception on throw.

## 2. Backend — endpoint & registration

- [ ] 2.1 Register the handler in `GamesModuleRegistration.AddGamesModule()` (`AddScoped<IQueryHandler<CheckAppVersionQuery, AppVersionCheckResult>, CheckAppVersionQueryHandler>()`).
- [ ] 2.2 Add `POST /games/version-checker` in the Games API — map it **anonymous** (outside the `RequireAuthorization()` group, or `.AllowAnonymous()`), parse/validate the body, dispatch the query.
- [ ] 2.3 Map results to HTTP: `204 No Content` for `UpToDate`, `409 Conflict` for `UpdateRequired`, `400 Bad Request` (validation problem) for a malformed/unparseable version. Declare `.Produces(204/409)` and `.ProducesValidationProblem()`.
- [ ] 2.4 Add the `Games:MinimumAppVersion` key to Azure App Configuration (and document it / dev appsettings) — leave unset/empty by default so the gate stays dormant.

## 3. Backend — tests

- [ ] 3.1 Create `Tests/CheckAppVersion/` mirroring the feature slice.
- [ ] 3.2 Handler test: returns `UpdateRequired` when client version < minimum (`Handle_ShouldReturnUpdateRequired_WhenBelowMinimum`).
- [ ] 3.3 Handler test: returns `UpToDate` when client version ≥ minimum, including the `1.10.0` vs `1.9.0` numeric-ordering case.
- [ ] 3.4 Handler test: returns `UpToDate` when the minimum key is absent/empty.
- [ ] 3.5 Handler test: malformed version is rejected/parsed-as-invalid (drives the endpoint's 400 path).
- [ ] 3.6 Use xUnit + Moq + Bogus; add a faker/factory under `Tests/Factories/` if helpful. Run `dotnet test src/Games/HexMaster.ThePrey.Games.Tests/`.

## 4. Client — service & gate

- [ ] 4.1 Add `checkAppVersion(version: string)` to `src/ThePrey` `games.service.ts`: `POST {apiBase}/version-checker` with body `{ 'current-version': version }`; resolve to a result indicating only whether the response was `409`. Swallow non-409 errors (return "ok") so the gate fails open.
- [ ] 4.2 In `home.page.ts`, add an `updateRequired` signal; call `checkAppVersion(this.appVersion())` in `ngOnInit` after the version is resolved, and set `updateRequired` true only on a 409.
- [ ] 4.3 In `home.page.html`, bind `[disabled]` of all main-menu `ion-button`s to `updateRequired()` (combined with existing disable conditions) and render an "update in the store" banner when `updateRequired()`.
- [ ] 4.4 Add i18n strings for the update-required title/body to the translation resource files.

## 5. Client — tests & verification

- [ ] 5.1 Add/extend `home.page.spec.ts` to assert: 409 → `updateRequired` true and buttons disabled; 204/404/error → menu enabled.
- [ ] 5.2 Add a `games.service` test for `checkAppVersion` mapping (409 → update-required, other → ok).
- [ ] 5.3 Run the client unit tests and lint for `src/ThePrey`.

## 6. End-to-end validation

- [ ] 6.1 With `Games:MinimumAppVersion` unset, confirm the endpoint returns `204` and the menu is enabled.
- [ ] 6.2 Set the key above the client version, confirm `409` and that the main menu disables with the update message shown.
- [ ] 6.3 Confirm fail-open: stop the backend / point at an older one (404) and verify the menu stays enabled.
