# The Prey — Developer Guide for Claude Code

## Project Overview

**The Prey** is a location-based multiplayer game. Players create and edit playfields (GPS polygon areas); game sessions happen inside those playfields. This repository contains the backend (a modular-monolith ASP.NET Core 10 API hosted on Azure, orchestrated with .NET Aspire) the promotional website, and the client app: a native Android app written in Kotlin that consumes the REST and real-time APIs.

## Repository Layout

```
app/
└── src/                                 # Native Android client app (Kotlin)
server/
├── infrastructure/                      # Bicep: landing-zone, shared modules, per-module APIs
└── src/
    ├── Aspire/
    │   ├── ThePrey.Aspire.AppHost/          # Aspire orchestration entry point
    │   └── ThePrey.Aspire.ServiceDefaults/  # Shared OpenTelemetry + health check wiring
    ├── Core/
    │   └── HexMaster.ThePrey.Core/          # ICommandHandler / IQueryHandler interfaces
    ├── PlayFields/                          # PlayFields domain module (see module layout below)
    ├── Games/                               # Games domain module
    ├── Users/                               # Users domain module
    └── Shared/
        ├── ThePrey.Core/
        └── ThePrey.Core.Tests/
website/
├── infrastructure/                      # Bicep: Static Web App
└── src/                                 # Hugo site (theprey.nl)
```

### Per-Domain Module Layout

Each domain (`PlayFields`, `Games`, `Users`) follows:

```
HexMaster.ThePrey.{Domain}/               # Domain logic, CQRS handlers, repository interface
HexMaster.ThePrey.{Domain}.Abstractions/  # Public DTOs in DataTransferObjects/
HexMaster.ThePrey.{Domain}.Api/           # Minimal API endpoints
HexMaster.ThePrey.{Domain}.Data.*/        # Data adapter (Table Storage, Postgres, etc.)
HexMaster.ThePrey.{Domain}.Tests/         # Unit tests mirroring feature slices
```

---

## Server-Side Work — MUST Read Guidelines First

**Before implementing any server-side feature, you MUST consult the hexmaster-coding-guidelines MCP server.**

Use the MCP tools `mcp__hexmaster-coding-guidelines__list_docs` and `mcp__hexmaster-coding-guidelines__get_doc` to fetch the relevant ADRs and recommendations. Key documents to check:

| Document ID | When to Read |
|---|---|
| `0002-modular-monolith-structure` | Adding new modules or projects |
| `0004-cqrs-recommendation-for-aspnet-api` | Any command/query handler work |
| `0005-minimal-apis-over-controllers` | Adding or modifying API endpoints |
| `0007-vertical-slice-architecture` | Structuring new features |
| `0008-adopt-opentelemetry-for-observability` | **Every new handler** — OTel is mandatory |
| `0009-feature-slices-module-structure` | Physical file layout inside a module |
| `unit-testing-xunit-moq-bogus` | Writing unit tests |

### Authentication & Authorization

Every new API module **must** follow this exact pattern:

1. Call `builder.AddDefaultAuthentication()` in `Program.cs` (after `builder.AddServiceDefaults()`).
2. Call `app.UseAuthentication()` then `app.UseAuthorization()` in the middleware pipeline **before** any `app.Map*Endpoints()` call.
3. Use `.RequireAuthorization()` on endpoint `MapGroup(...)` so all routes in the group are protected by default.

```csharp
// Program.cs — correct order
builder.AddServiceDefaults();
builder.AddDefaultAuthentication();      // ← MUST be here
// ...
app.UseAuthentication();                 // ← MUST be before Map*
app.UseAuthorization();
app.MapMyModuleEndpoints();
```

The caller's identity is available in handlers via the `sub` claim (`principal.FindFirstValue("sub")`). `MapInboundClaims = false` is set in `ServiceDefaults`, so claims keep their JWT names.

### CQRS Pattern

All application logic uses a lightweight CQRS pattern via `ICommandHandler<TCommand, TResult>` and `IQueryHandler<TQuery, TResult>` from `HexMaster.ThePrey.Core`. **Never use MediatR.**

- Commands and queries are `sealed record` types internal to the module's `Features/{FeatureName}/` namespace.
- DTOs (`*Request`, `*Dto`) are `sealed record` types in the `Abstractions/DataTransferObjects/` project.
- Endpoints in the `.Api` project receive DTOs, map to commands/queries, dispatch to handlers, map results to HTTP responses. No business logic in endpoints.
- Register handlers in `{Domain}ModuleRegistration.cs` using `services.AddScoped<ICommandHandler<...>, ...>()`.

### OpenTelemetry (Mandatory)

Every new handler **must** instrument with OpenTelemetry:

```csharp
using var activity = PlayFieldActivitySource.Source.StartActivity("FeatureName");
activity?.SetTag("playfield.owner_id", command.OwnerId);
// ...
catch (Exception ex)
{
    activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
    activity?.AddException(ex);
    throw;
}
```

- Activity sources live in `{Domain}/Observability/` (e.g., `PlayFieldActivitySource.cs`).
- Metrics counters and histograms live in `IPlayFieldMetrics` / `PlayFieldMetrics.cs` in the same folder.
- Use low-cardinality tag values. Never tag with user IDs or free-form strings.
- Name the source and meter `HexMaster.ThePrey.{Domain}`: ServiceDefaults registers that prefix and exports to the Aspire dashboard locally and Application Insights in Azure. Never add `AddOpenTelemetry()` to a module's `Program.cs`.

### Unit Tests

- Use **xUnit + Moq + Bogus**. No other test libraries without an ADR.
- Test project mirrors feature slices: `Tests/CreatePlayField/`, `Tests/UpsertPlayField/`, etc.
- Test naming: `Method_ShouldExpected_WhenCondition`.
- Test factories live in `Tests/Factories/` (e.g., `PlayFieldFaker.cs`).
- Target ≥80% coverage for the domain and handler code.
- Run tests: `dotnet test server/src/PlayFields/HexMaster.ThePrey.PlayFields.Tests/`

---

## Build & Test Commands

```powershell
# Build a server module (PlayFields example)
dotnet build server/src/PlayFields/HexMaster.ThePrey.PlayFields.Tests/HexMaster.ThePrey.PlayFields.Tests.csproj

# Run server unit tests
dotnet test server/src/PlayFields/HexMaster.ThePrey.PlayFields.Tests/

# Build the full backend solution
dotnet build server/src/the-prey.slnx

# Android app — run from app/src (the Gradle root; open this folder in Android Studio)
./gradlew assembleDebug
./gradlew lintDebug testDebugUnitTest
```

---

## Releases & versioning

- **One version source:** `.github/workflows/version.yml` is the only place GitVersion runs (configured by `GitVersion.yml`). Every workflow that needs a version calls it as a job and reads `needs.version.outputs.*`; never run GitVersion anywhere else.
- **One release step:** `.github/actions/create-release/action.yml` is the only place that creates tags and GitHub releases. It takes a `version`, creates tag + release `v<semVer>` only when that release does not exist yet, and outputs `tag` and `created`. Callers need a checkout and `permissions: contents: write`. The backend workflows (`games.yml`, `notifications.yml`, `playfields.yml`, `users.yml`) call it after a successful deploy on pushes to `main`.
- **Shared version line:** backend services and the app share `v<semVer>`; whichever pipeline finishes first on a commit creates the release, the others no-op.
- **App `versionCode`:** `major * 1000000 + minor * 1000 + patch + VERSION_CODE_OFFSET` from the `version.yml` outputs (`1.1.8` → `1001008`), with `versionName` = `semVer`. Signing and Play publishing need the secrets `KEYSTORE_BASE64`, `KEYSTORE_PASSWORD`, `KEY_PASSWORD`, `KEY_ALIAS` and `PLAY_SERVICE_ACCOUNT_JSON`.

---

## Android App (`app/src`)

- **Stack:** Kotlin + Jetpack Compose (Material 3), single-activity, Gradle Kotlin DSL with a version catalog (`gradle/libs.versions.toml`). AGP 9 with its built-in Kotlin support, so the `kotlin-android` plugin is not applied.
- **Application id:** `nl.hexmaster.theprey`. This is the id of the previously published app, so releases update the existing Play Store listing. Do not change it.
- **Versioning:** `versionCode`/`versionName` come from Gradle properties (`ORG_GRADLE_PROJECT_versionCode` / `_versionName` in CI); see "App releases" below. Play rejects any upload whose `versionCode` is not higher than the last published one.
- **Release signing:** configured only when `app/src/key.properties` exists (`storeFile`, `storePassword`, `keyAlias`, `keyPassword`). Never commit it or a keystore.
- **Theme:** `ui/theme/` holds the design-system tokens (`Color.kt`), fonts (`Type.kt`: Special Elite for headings/numbers, PT Mono for body/labels) and `ThePreyTheme`. Screens read colors through `MaterialTheme.colorScheme` or `ThePreyTheme.colors`, never hard-coded values. See the `the-prey-design-system` skill.
- **Logo & splash:** `res/drawable/ic_prey_mark.xml` is the one mark vector (108 dp adaptive-icon grid, mark in the central 66 dp) shared by the launcher icon, the system splash (`avd_splash_mark.xml`, 800 ms, set in `Theme.ThePrey.Starting`) and Compose (`ui/components/PreyMark`, which sizes it by the visible mark). On a cold start `ui/splash/LaunchSequence` continues from the splash's last frame and then crossfades into the session-routed nav host; all timing lives in the pure `LaunchTimeline` (constants + `frameAt`, unit-tested), and `LaunchSequenceGate` keeps it to once per process.
- **Strings:** all user-facing text lives in `res/values/strings.xml` with Dutch in `res/values-nl/strings.xml`. Every text change in the app **and** the website must land in both English and Dutch — follow the `the-prey-multilingual` skill.
- **Realtime:** follow the `the-prey-realtime-communication` skill and `docs/api/realtime.md`: state changes go over HTTP only, and live updates arrive on one Web PubSub socket owned by a single game-state service.
- **Update gate (`app-version-gate`):** `data/version/AppVersionGate` posts `BuildConfig.VERSION_NAME` to `POST /games/version-checker` on start, on every return to the foreground and every 10 minutes while visible (loop in `MainActivity`). Only a `409` closes the gate; any other status or error leaves the app usable (fail open), and debug builds never block. `ui/update/UpdateGate` then shows `ui/update/UpdateRequiredScreen` (Play Store link via `BuildConfig.PLAY_STORE_URL`) over every route except `LOBBY` and `GAME`: a player in a lobby or running game finishes it and is blocked when they leave it.

### App releases

- **Workflow:** `.github/workflows/android-release.yml`. Pull requests and pushes to `main` touching `app/src/**` run `./gradlew lintDebug testDebugUnitTest`; pushes to `main` (and manual runs on `main`) then build, sign and publish.
- **Internal track only:** the bundle goes to the Play Console **internal testing** track for `nl.hexmaster.theprey` as a completed release. Promotion is never a new build (see *Update policy and promotion* below).
- **Version:** `versionName` = `semVer` from `version.yml` (the run fails on an empty or pre-release version); `versionCode = major * 1000000 + minor * 1000 + patch + VERSION_CODE_OFFSET`, where the repository variable `VERSION_CODE_OFFSET` (`vars.VERSION_CODE_OFFSET`, default `0`) lifts the line above a legacy code already in Play.
- **Release notes:** the Copilot CLI turns the `app/src` commits since the last successful run into English and Dutch "What's new" texts (`whatsnew/whatsnew-en-US`, `whatsnew/whatsnew-nl-NL`, max 500 characters each, cut at a line break). When Copilot fails, the commit subjects are used (Dutch prefixed with "Verbeteringen en bugfixes.") and the run shows a warning. The same texts form the body of the GitHub release `v<semVer>`, created by `.github/actions/create-release` after the Play upload.
- **Secrets:** `KEYSTORE_BASE64`, `KEYSTORE_PASSWORD`, `KEY_PASSWORD`, `KEY_ALIAS` (upload key), `PLAY_SERVICE_ACCOUNT_JSON` (service account with release rights) and `COPILOT_GITHUB_TOKEN` (fine-grained PAT with the Copilot Requests permission).

### Update policy and promotion

- **Label every release PR:** each pull request into `main` (normally `development` → `main`) carries exactly one of `update:optional` or `update:required`; `.github/workflows/pr-update-label.yml` fails otherwise and is a required check on `main`. `update:required` means players outside a game must update once this release reaches production.
- **Internal publish never forces:** `android-release.yml` ignores the labels; `Games:MinimumAppVersion` only changes at promotion.
- **Promote:** run `.github/workflows/android-promote.yml` with input `version` (`x.y.z`, tag `v<version>` must exist and be higher than the last `app-production-v*` tag). It promotes that build from the internal track to production with fastlane and tags `app-production-v<version>`.
- **Policy over all PRs since the last promotion:** a required promotion is one where any PR merged into `main` (first-parent) since the last `app-production-v*` tag is labelled `update:required`; unlabelled PRs count as optional. The run summary lists the PRs and the decision.
- **Enforce:** for a required promotion the `enforce` job waits for the `play-production` environment. Approve it only once the Play Console shows the production release as Available; it then deploys `server/infrastructure/games/modules/games-app-config.bicep` with `minimumAppVersion` = the promoted version. Promoted versions only go up, so the minimum never goes down. The Games infrastructure deploy no longer writes the key.
- **Shared offset:** both workflows read the repository variable `VERSION_CODE_OFFSET`, so they compute the same `versionCode`.
- **Rollback:** lower or clear `Games:MinimumAppVersion` (same Bicep module, or the portal); the APIs refresh key-values every 5 minutes and open apps re-check every 10.

---

## Key Architectural Decisions

- **Native Android client** — the only client is the Kotlin Android app in `app/src`. There is no web, iOS, or cross-platform client; earlier Ionic and .NET MAUI attempts were removed.
- **No MediatR** — uses custom `ICommandHandler` / `IQueryHandler` from `HexMaster.ThePrey.Core`.
- **No controller-based APIs** — all endpoints use ASP.NET Core Minimal APIs.
- **Repository interfaces** belong in the module project (not Abstractions); only cross-module service ports go in Abstractions.
- **Last-write-wins** on `LastUpdatedOn` — server rejects stale writes with 409 so offline-capable clients can reconcile.
