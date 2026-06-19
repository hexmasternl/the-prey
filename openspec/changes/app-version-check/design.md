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
- Client gate on the main screen that, on a 409, disables all menu actions and shows an
  "update in the store" message.
- Fail-open: any non-409 outcome (204, 404, network/parse error) leaves the menu fully usable.

**Non-Goals:**
- No store-listing deep links or auto-update flow — the message is informational only.
- No platform-specific (iOS vs Android) minimum versions — a single minimum applies to all.
- No forced re-check loop or polling — the check runs once on home-screen load.
- No gating of any other screen or endpoint — only the main menu reacts.

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

### 5. Client gate: run once on home load, react only to 409
`GamesService.checkAppVersion(version)` POSTs the body and resolves to a small result the page
can switch on. The home page calls it in `ngOnInit` (alongside the existing active-game and
version reads) and sets an `updateRequired` signal **only** on an HTTP 409. The template:
- binds `[disabled]` of every menu `ion-button` to `updateRequired()` (combined with existing
  disable conditions), and
- renders an "update the app in the store" banner (new i18n strings) when `updateRequired()`.
All other outcomes — 204, 404 (older backend without the endpoint), 400, or a network error —
leave `updateRequired()` false. The service swallows non-409 errors (mirrors `getActiveGame`'s
`catchError(() => of(...))` pattern) so the gate fails open.

## Risks / Trade-offs

- **Misconfigured minimum locks out all users** → Mitigation: the key is operator-controlled and
  changeable at runtime via App Configuration with no redeploy; a bad value can be reverted in
  seconds. Document the expected `x.x.x` format.
- **Web build has no native version** (`App.getInfo()` throws off-native) → Mitigation: the home
  page already falls back to `environment.version`; the client sends whatever version it has, and
  any parse failure on the server returns 400 → fail open. The gate primarily targets native
  builds.
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
