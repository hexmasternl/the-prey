## Why

There is no way to force players onto a minimum supported client version. As the backend
contract evolves (game engine, real-time payloads, scoring), an outdated client can silently
break or corrupt a live game. We need a server-controlled kill switch that blocks outdated
apps from starting a game and tells the user to update, without shipping a new client.

## What Changes

- Add a new **`POST /games/version-checker`** endpoint to the Games API that accepts the
  client's current version (`{ "current-version": "x.x.x" }`) and compares it against a
  minimum-supported version held in Azure App Configuration.
  - Returns **204 No Content** when the client version is greater than or equal to the
    configured minimum (app is fine).
  - Returns **409 Conflict** when the client version is lower than the configured minimum
    (app must be updated).
- The minimum version is read from Azure App Configuration (already wired into the Games API
  via `AddServiceDefaults` / `UseAppConfigurationRefresh`), so it can be changed at runtime
  with no redeploy.
- On the client **home (main) screen** and the **game-join screen**, the app posts its local
  version to the endpoint on load. The page's action buttons are **disabled by default** and
  only become usable once the check resolves:
  - The buttons start **disabled** until the version check returns.
  - Any non-409 outcome (204, 404 for a server that predates the endpoint, a network error, or
    any other error) **enables** the buttons — the gate fails open so a backend hiccup never
    bricks the app.
  - **Only a 409** keeps the buttons disabled and shows a clear "update the app" message that
    includes a link to the Play Store.

## Capabilities

### New Capabilities
- `app-version-gate`: Server endpoint that validates a client's version against a configured
  minimum, and the client behavior that disables the main menu and prompts to update when the
  client is below the minimum.

### Modified Capabilities
<!-- None — no existing spec's requirements change. -->

## Impact

- **Backend** — `HexMaster.ThePrey.Games` (new `Features/CheckAppVersion` slice + handler),
  `HexMaster.ThePrey.Games.Abstractions` (request DTO), `HexMaster.ThePrey.Games.Api`
  (new endpoint on the `/games` group), `GamesModuleRegistration` (handler registration),
  `HexMaster.ThePrey.Games.Tests` (handler unit tests).
- **Configuration** — a new Azure App Configuration key holding the minimum supported app
  version (e.g. `Games:MinimumAppVersion`).
- **Client** — `src/ThePrey` `games.service.ts` (new `checkAppVersion` call), `home.page.ts` /
  `home.page.html` and `game-join.page.ts` / `game-join.page.html` (disabled-by-default version
  gate + update banner with Play Store link), `environment.ts` (Play Store URL), i18n resource
  files (new strings for the update message and store link).
- **No breaking changes** — the gate fails open on any non-409 outcome; existing clients are
  unaffected until the minimum version is configured above their version. Buttons are only
  briefly disabled while the check is in flight.
