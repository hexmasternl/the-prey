## 1. Backend — DTO & feature slice

- [x] 1.1 Add `CheckAppVersionRequest` record to `HexMaster.ThePrey.Games.Abstractions/DataTransferObjects/` with a `current-version` property (JSON name `current-version`).
- [x] 1.2 Create `Features/CheckAppVersion/CheckAppVersionQuery.cs` (`sealed record` carrying the parsed/raw client version).
- [x] 1.3 Create a result type for the query (e.g. `AppVersionCheckResult` enum: `UpToDate`, `UpdateRequired`).
- [x] 1.4 Create `Features/CheckAppVersion/CheckAppVersionQueryHandler.cs` implementing `IQueryHandler<CheckAppVersionQuery, AppVersionCheckResult>`: read minimum from `IConfiguration` key `Games:MinimumAppVersion`, numeric component-wise compare, return result. Treat absent/empty key as `UpToDate`.
- [x] 1.5 Add OpenTelemetry: start a `CheckAppVersion` activity via `GameActivitySource.Source`, tag `version.outcome` (`up_to_date`/`update_required`), set error status + add exception on throw.

## 2. Backend — endpoint & registration

- [x] 2.1 Register the handler in `GamesModuleRegistration.AddGamesModule()` (`AddScoped<IQueryHandler<CheckAppVersionQuery, AppVersionCheckResult>, CheckAppVersionQueryHandler>()`).
- [x] 2.2 Add `POST /games/version-checker` in the Games API — map it **anonymous** (outside the `RequireAuthorization()` group, or `.AllowAnonymous()`), parse/validate the body, dispatch the query.
- [x] 2.3 Map results to HTTP: `204 No Content` for `UpToDate`, `409 Conflict` for `UpdateRequired`, `400 Bad Request` (validation problem) for a malformed/unparseable version. Declare `.Produces(204/409)` and `.ProducesValidationProblem()`.
- [x] 2.4 Add the `Games:MinimumAppVersion` key to Azure App Configuration (and document it / dev appsettings) — leave unset/empty by default so the gate stays dormant.

## 3. Backend — tests

- [x] 3.1 Create `Tests/CheckAppVersion/` mirroring the feature slice.
- [x] 3.2 Handler test: returns `UpdateRequired` when client version < minimum (`Handle_ShouldReturnUpdateRequired_WhenBelowMinimum`).
- [x] 3.3 Handler test: returns `UpToDate` when client version ≥ minimum, including the `1.10.0` vs `1.9.0` numeric-ordering case.
- [x] 3.4 Handler test: returns `UpToDate` when the minimum key is absent/empty.
- [x] 3.5 Handler test: malformed version is rejected/parsed-as-invalid (drives the endpoint's 400 path).
- [x] 3.6 Use xUnit + Moq + Bogus; add a faker/factory under `Tests/Factories/` if helpful. Run `dotnet test src/Games/HexMaster.ThePrey.Games.Tests/`.

## 4. Client — service & config

- [x] 4.1 Add `checkAppVersion(version: string)` to `src/ThePrey` `games.service.ts`: `POST {apiBase}/version-checker` with body `{ 'current-version': version }`; resolve to `'update-required'` on a `409` and `'ok'` for every other outcome. Swallow all non-409 errors (`catchError(() => of('ok'))`) so the gate fails open.
- [x] 4.2 Add a `playStoreUrl` field to `environment.ts` and `environment.prod.ts` (the app's Play Store listing URL).

## 5. Client — home screen gate

- [x] 5.1 In `home.page.ts`, add `versionChecked` (default `false`) and `updateRequired` (default `false`) signals plus `versionBlocked = computed(() => !versionChecked() || updateRequired())`; call `checkAppVersion(this.appVersion())` in `ngOnInit` and in `finally` set `versionChecked` true, setting `updateRequired` true only on `'update-required'`.
- [x] 5.2 In `home.page.html`, OR `versionBlocked()` into the `[disabled]` of every main-menu `ion-button` (so they are disabled by default until the check resolves).
- [x] 5.3 In `home.page.html`, render an "update the app" banner with a Play Store link/button when `updateRequired()`; wire the link to open `environment.playStoreUrl` via `Browser.open` (Capacitor Browser, already imported).

## 6. Client — game-join screen gate

- [x] 6.1 In `game-join.page.ts`, add the same `versionChecked` / `updateRequired` / `versionBlocked` signals; call `checkAppVersion` in `ionViewWillEnter` and resolve the gate in `finally`.
- [x] 6.2 Fold `!versionBlocked()` into the `canJoin` computed so the Join button is disabled by default until the check resolves and stays disabled on a 409.
- [x] 6.3 In `game-join.page.html`, render the "update the app" banner with the Play Store link when `updateRequired()` (reusing the shared i18n strings).

## 7. Client — i18n & tests

- [x] 7.1 Add i18n strings for the update-required title/body and the Play Store link label to every translation resource file.
- [x] 7.2 Extend `home.page.spec.ts`: buttons disabled before the check resolves; 409 → stay disabled + banner shown; 204/404/error → enabled.
- [x] 7.3 Add a `game-join.page` spec covering the same disabled-by-default / 409 / fail-open behavior on `canJoin`.
- [x] 7.4 Add a `games.service` test for `checkAppVersion` mapping (409 → `'update-required'`, other/error → `'ok'`).
- [x] 7.5 Run the client unit tests and lint for `src/ThePrey`.

## 8. End-to-end validation

- [ ] 8.1 With `Games:MinimumAppVersion` unset, confirm the endpoint returns `204` and both screens' buttons enable after the check.
- [ ] 8.2 Set the key above the client version, confirm `409` and that both the home menu and the join button stay disabled with the update message + Play Store link shown.
- [ ] 8.3 Confirm fail-open: stop the backend / point at an older one (404) and verify both screens' buttons enable.
- [ ] 8.4 Confirm disabled-by-default: on a slow/pending check the buttons are disabled until the response arrives.
