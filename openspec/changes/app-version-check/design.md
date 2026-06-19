## Context

The Games API is an ASP.NET Core 10 Minimal-API module in the modular monolith. It already
binds Azure App Configuration through `AddServiceDefaults()` and refreshes it at runtime via
`app.UseAppConfigurationRefresh()` in `Program.cs`, so configured values are available through
`IConfiguration` and update without a redeploy.

The client is the Ionic/Angular app in `src/ThePrey`. Its home (main) screen already reads the
native app version (`App.getInfo().version`, a GitVersion semVer baked into the bundle) and
renders the action buttons (Play Now / Resume Game, Playfields, Settings, Logout, Quit) only
after the user profile loads. The `authTokenInterceptor` attaches the Auth0 Bearer token to
every request hitting the API origin. There is currently no mechanism to refuse outdated clients.

This change spans both the Games module and the client, which is why a design doc is warranted:
it pins the version-comparison rules, the wire contract, and the fail-open behavior so the two
sides agree.

## Goals / Non-Goals

**Goals:**
- Server-side, runtime-configurable minimum supported client version with no redeploy required.
- A single `POST /games/version-checker` endpoint that returns 204 (ok) or 409 (must update).
- Client gate on the **home** and **game-join** screens whose action buttons are disabled by
  default, enabled once the check resolves to anything other than 409, and kept disabled on a
  409 alongside an "update the app" message with a Play Store link.
- Fail-open: any non-409 outcome (204, 404, network/parse error) enables the buttons.

**Non-Goals:**
- No auto-update flow — the message links to the Play Store but does not install.
- No platform-specific (iOS vs Android) minimum versions — a single minimum applies to all.
  (The store URL is configurable, leaving room for an App Store URL later.)
- No forced re-check loop or polling — the check runs once per page load.
- No gating of any screen beyond home and game-join.

## Decisions

### 1. Version model: three-part numeric `major.minor.patch`
The request body is `{ "current-version": "x.x.x" }` where each `x` is a non-negative integer.
The server parses it with `System.Version` (or a small explicit parser) and compares
component-by-component against the configured minimum. A version is "below minimum" when its
ordered (major, minor, patch) tuple compares less than the configured minimum's tuple.
- *Why not string compare?* `"1.10.0"` vs `"1.9.0"` would sort wrong lexically; numeric tuple
  comparison is correct.
- *Malformed input* (missing parts, non-numeric, null/empty) → `400 Bad Request` via the
  endpoint's validation path, distinct from the 409 "must update" signal. The client treats a
  400 the same as any non-409: fail open.

### 2. Endpoint contract and placement
`POST /games/version-checker` lives in the Games API. It returns:
- **204 No Content** when client version ≥ minimum.
- **409 Conflict** when client version < minimum.
- **400** for an unparseable version (validation problem).
The route is **anonymous** (no `RequireAuthorization()`), because a version gate must work
independently of auth state and should never be masked by a token problem. It is therefore
mapped outside the authenticated `/games` group (e.g. a small dedicated `MapGroup("/games")`
without `RequireAuthorization`, or `.AllowAnonymous()` on the single route).
- *Why anonymous?* The gate is a pre-condition for using the app at all; coupling it to a valid
  Auth0 token would let an auth hiccup hide a required-update state, or block the check before
  login. The endpoint reveals only a configured minimum version — no user data.

### 3. CQRS query, not a command
Version checking is a read with no side effects, so it is modeled as an
`IQueryHandler<CheckAppVersionQuery, AppVersionCheckResult>` in a `Features/CheckAppVersion`
slice. The handler reads the minimum from `IConfiguration`, compares, and returns an enum-like
result (`UpToDate` / `UpdateRequired`). The endpoint maps that to 204/409. OpenTelemetry
instrumentation is mandatory: a `CheckAppVersion` activity tagged with low-cardinality outcome
(`version.outcome` = up_to_date | update_required) — never the raw version string as a metric
dimension (high cardinality).

### 4. Minimum version source: Azure App Configuration key
A single key, e.g. `Games:MinimumAppVersion`, holds the minimum as `"x.x.x"`. Read via
`IConfiguration`/`IOptionsMonitor` so `UseAppConfigurationRefresh` picks up changes at runtime.
- *Missing/empty key* → treat as "no minimum configured" → every client is up to date (204).
  This keeps the feature dormant and fail-open until an operator sets the key.

### 5. Client gate: disabled by default, applied to home AND game-join, react only to 409
`GamesService.checkAppVersion(version)` POSTs the body and resolves to a small result the pages
can switch on (e.g. `'ok' | 'update-required'`). Both the **home** page and the **game-join**
page run the check on load (home: `ngOnInit`; join: `ionViewWillEnter`) and model a tri-state
gate via two signals:
- `versionChecked` — starts `false`, set `true` once the check resolves *in any way*.
- `updateRequired` — set `true` **only** on an HTTP 409.

The gate is **disabled by default**: action buttons are disabled while `!versionChecked()`
(check in flight) and stay disabled when `updateRequired()`. They become usable the moment the
check resolves to anything other than 409. Concretely each page exposes a
`versionBlocked = computed(() => !versionChecked() || updateRequired())` that is OR-ed into every
button's existing `[disabled]` condition (home's menu buttons; join's `canJoin`).

The service swallows **all non-409 errors** (mirroring `getActiveGame`'s
`catchError(() => of(...))` pattern) and resolves to `'ok'`, so 204, 404 (older backend without
the endpoint), 400, and network errors all enable the buttons — the gate fails open. The brief
disabled window while the request is in flight is the only added latency, and it overlaps the
existing profile/active-game loads that already gate the buttons.

When `updateRequired()`, each page renders an "update the app" banner (new i18n strings) with a
button/link that opens the Play Store via `Browser.open({ url })` (Capacitor Browser, already
used by the home page).

### 6. Play Store link via configured URL
The store link target is read from `environment.playStoreUrl` (a new field, set per build/flavor)
rather than hard-coded, so the package id lives in one place and a future iOS App Store URL can
be added without touching page logic. On native, the link opens with Capacitor `Browser.open`;
on the web it falls back to the same URL.
- *Why config, not hard-coded?* The Play Store URL embeds the app's package id, which is a
  deployment detail, not page logic.

## Risks / Trade-offs

- **Misconfigured minimum locks out all users** → Mitigation: the key is operator-controlled and
  changeable at runtime via App Configuration with no redeploy; a bad value can be reverted in
  seconds. Document the expected `x.x.x` format.
- **Web build has no native version** (`App.getInfo()` throws off-native) → Mitigation: the home
  page already falls back to `environment.version`; the client sends whatever version it has, and
  any parse failure on the server returns 400 → fail open. The gate primarily targets native
  builds.
- **Disabled-by-default delays the buttons until the check returns** → Mitigation: the check runs
  in parallel with the existing profile/active-game loads that already gate the buttons, so it
  adds no perceptible delay; any error resolves the gate immediately to enabled.
- **Fail-open means a compromised/blocked endpoint can't enforce updates** → Accepted trade-off:
  bricking the app on any backend or network failure is worse than briefly allowing an outdated
  client. Enforcement is best-effort by design.
- **Two-part vs three-part versions** → The parser must tolerate the project's GitVersion semVer
  shape; pre-release/build suffixes (if any) are stripped before numeric comparison. Covered by
  unit tests.

## Migration Plan

1. Ship the endpoint and client gate with **no** `Games:MinimumAppVersion` key set → feature is
   dormant (always 204), zero behavioral change for existing users.
2. Once a client release is widely adopted, set `Games:MinimumAppVersion` in Azure App
   Configuration to the desired floor. Refresh propagates it without redeploy.
3. **Rollback**: clear or lower the `Games:MinimumAppVersion` key — the gate immediately returns
   204 for all clients again.

## Open Questions

- Exact App Configuration key name (`Games:MinimumAppVersion` proposed) — confirm against any
  existing Games config naming convention.
- Whether pre-release/build-metadata suffixes can appear in the client `version` string and must
  be stripped, or whether it is always clean `x.x.x`.
