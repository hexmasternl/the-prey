---
name: the-prey-api-contract-sync
description: Keeps The Prey's server HTTP contracts and the Android client in step — DTOs, routes, status codes, problem `code` values, JSON naming and nullability, the 409 last-write-wins rule on LastUpdatedOn, and backward compatibility with older app versions still installed. Use whenever you add, rename, remove or change a request/response DTO in any *.Abstractions/DataTransferObjects folder, an endpoint route, method or status code in a *.Api project, a machine-readable error code, or the matching Retrofit interface, *Dto.kt or repository result mapping in app/src/.../data/ — and whenever you update docs/api/endpoints.md.
metadata:
  author: the-prey
  version: "1.0"
---

# The Prey — API Contract Sync (server ⇄ Android)

The server and the app are built and released separately, and old app versions stay installed for weeks. A contract change is only done when **both sides, the docs and the tests** say the same thing, and an app that is one or more releases behind keeps working.

## The rules

1. **One change, both sides.** A DTO, route, status code or error `code` change lands in the server, the Kotlin client, `docs/api/endpoints.md` and the tests in the same change (or the app side follows in the next PR, with the server change made backward compatible first).
2. **Additive by default.** Add optional fields; never rename, retype or remove a field or route an installed app uses. If a break is unavoidable, keep the old shape alongside the new one until the app version gate has moved everyone on (see "Breaking changes").
3. **The server leads.** Clients apply what the server returns (`replaceGame`, `SaveOutcome.Saved`) and never trust their own copy over it.
4. **Status codes and `code` values are contract.** The app maps them to result types and localized strings; it never shows the server's English `detail`.

## Where the contract lives

| Concern | Server | Android (`app/src/app/src/main/java/nl/hexmaster/theprey/`) |
|---|---|---|
| Game DTOs | `server/src/Games/HexMaster.ThePrey.Games.Abstractions/DataTransferObjects/` (`GameDto.cs`, `ParticipantDto.cs`, `FullGameStateDto.cs`, `GameStatusDto.cs`, `GameStateDto.cs`, `TagCandidatesDto.cs`, `Requests.cs`, `CheckAppVersionRequest.cs`, `GameNotificationConnectionDto.cs`) | `data/game/GameStateDto.kt`, `ActiveGameDto.kt`, `CreateGame.kt`, `JoinGame.kt`, `Tagging.kt` |
| Game routes | `server/src/Games/HexMaster.ThePrey.Games.Api/Endpoints/GameEndpoints.cs` | `data/game/GamesApi.kt` (+ test base `app/src/app/src/test/.../data/game/UnusedGamesApi.kt`) |
| Game result mapping | endpoint `try/catch` → `Results.*`, `GameRuleProblem(code, …)` | `data/game/GameRepository.kt`, `CommandResult.kt` (`CommandResult`, `TokenResult`, `SnapshotResult`), `JoinGame.kt` (`joinRefusal`) |
| Domain error codes | `server/src/Games/HexMaster.ThePrey.Games/DomainModels/GameRuleException.cs` | `JoinProblemCodes` in `data/game/JoinGame.kt` |
| Playfield DTOs / routes | `server/src/PlayFields/HexMaster.ThePrey.PlayFields.Abstractions/DataTransferObjects/`, `...PlayFields.Api/Endpoints/PlayFieldEndpoints.cs` | `data/playfield/PlayFieldDto.kt`, `PlayFieldSummaryDto.kt`, `PlayFieldApi.kt`, `PlayFieldRepository.kt` |
| User DTOs / routes | `server/src/Users/HexMaster.ThePrey.Users.Abstractions/DataTransferObjects/`, `...Users.Api/Endpoints/UserEndpoints.cs` | `data/user/UserDto.kt`, `UserApi.kt`, `auth/SessionManager.kt` |
| Gateway prefixes | `server/infrastructure/landing-zone/modules/http-route-config.bicep`, YARP routes in `server/src/Aspire/ThePrey.Aspire.AppHost/AppHost.cs` | `BuildConfig.API_BASE_URL` (Retrofit paths are relative: `"games/{id}/start"`) |
| Human-readable contract | `docs/api/endpoints.md` (HTTP), `docs/api/realtime.md` (socket) | — |

Realtime payloads (`RealtimeMessages.cs` ⇄ `data/game/realtime/`) follow the same rules but have their own skill: `the-prey-realtime-communication`. Note that `configuration-changed` is decoded into the Kotlin `GameDto`, so `GameDto` changes affect both.

## JSON on the wire

**Server**: Minimal APIs with the ASP.NET Core defaults — there is no custom `JsonSerializerOptions`, `ConfigureHttpJsonOptions` or `JsonStringEnumConverter` anywhere. So:
- Properties are **camelCase**; reading is case-insensitive.
- **Enums are never put in DTOs.** Domain enums are mapped to strings in the mappers (`game.Status.ToString()`, `game.Outcome.ToString()` in `GameMappings.cs`). A raw enum would serialize as a number.
- `Guid` → lowercase string; `DateTimeOffset` → ISO-8601 with offset and up to 7 fractional digits.
- The only deliberate exception: `CheckAppVersionRequest` uses `[property: JsonPropertyName("current-version")]`.
- A missing JSON property binds to the C# default (`0`, `false`, `null`); request records put optional values last with defaults (`string? ProfilePictureUrl = null`).

**Android**: kotlinx.serialization, `Json { ignoreUnknownKeys = true }` (the `AppContainer` instance feeds Retrofit). Consequences:
- **Unknown server fields are ignored**, so the server may always add fields. Kotlin DTOs model only the subset the app uses (e.g. `ActiveGameDto(gameId)` out of `GameStatusDto`) — say so in the KDoc.
- **A field the server omits or sends as `null` breaks decoding** unless the Kotlin property has a default or is nullable. `coerceInputValues` is off, so `null` into `val x: String = ""` still fails. Give every non-essential response property a default, and make anything the server declares nullable (`Guid?`, `DateTimeOffset?`, `string?`) nullable in Kotlin.
- `encodeDefaults` is off: a **request** property with a Kotlin default is *not sent* when it equals the default. Leave request properties without defaults (see `CreateGameRequest`) unless omission is exactly what the server expects.
- Ids are `String`, timestamps are `String`. Keep `lastUpdatedOn` byte for byte as received; never parse and re-format it before sending it back.
- Status strings are constants (`GameStatuses`, `GameOutcomes`, `PlayerStates` in `GameStateDto.kt`) that must match the server enum names (`GameStatus`, `GameOutcome`, `PlayerState`).
- A body that fails to parse surfaces as a `RuntimeException` (`SerializationException`); repositories map it to the "failed" result, never crash.

## Status codes and errors

| Server answer | How the app reads it |
|---|---|
| `200`/`201` with body | success result with the body (`CommandResult.Ok(game)`, `SaveOutcome.Saved`, …) |
| `204` | bodiless success (`CommandResult.Left`, `TagResult.Tagged`, `DeleteOutcome.Deleted`) |
| `400` `ValidationProblem` (`errors` map) | generic failure, or for user settings the first message (`SessionManager.invalid`). Keys come from `nameof(...)`/`ParamName`, so they are often PascalCase — match case-insensitively |
| `400`/`404`/`409` `Problem` with `code` extension | `problemCode(body)` → specific result (`joinRefusal`) → localized string (`the-prey-multilingual`) |
| `401` | `TokenAuthenticator` renews once and retries; repositories never see it as a domain result |
| `403` | not allowed / no longer a member (`CommandResult.Forbidden`; `TokenResult.Forbidden`/`SnapshotResult.Forbidden` end the game session) |
| `404` | gone or not a member (`ActiveGameResult.None`, `LoadOutcome.NotFound`) |
| `409` | the state moved on (`CommandResult.Conflict`, `TagResult.NoLongerTaggable`) or a stale write (below) |
| `5xx`, `IOException` | transient: retryable failure |

Adding a machine-readable code: create a `GameRuleException` subclass with `Code => "snake_case"`, return it via `GameRuleProblem(ex.Code, ex.Message, status)` in the endpoint, add the constant and mapping on Android, add an English **and** Dutch string, and add a row to the status/`code` table in `docs/api/endpoints.md` (see the `POST /games/{id}/join` table).

### 409 last-write-wins (`LastUpdatedOn`)

`PUT /playfields/{id}` takes `UpsertPlayFieldRequest.LastUpdatedOn`. `UpsertPlayFieldCommandHandler` rejects with **409 and the stored `PlayFieldDto` as body** when `command.LastUpdatedOn < existing.LastModifiedOn`; on success the server stamps its own new time. The app echoes the exact `lastUpdatedOn` string it loaded, decodes the 409 body from `errorBody()` with the container's `Json` (`SaveOutcome.Conflict(current)`), and lets the user reconcile. Any new offline-editable resource follows the same pattern: client sends the version it started from, server compares, 409 carries the current version, the response carries the new stamp.

## Backward compatibility

- Old apps stay installed until `POST /games/version-checker` (anonymous, `CheckAppVersionQueryHandler`, `Games:MinimumAppVersion` in App Configuration; `204` up to date, `409` update required) tells them to update. The Android side of that gate is planned in `openspec/changes/app-demand-updates` and not implemented yet, so today **nothing forces an update** — design every server change for the oldest app in the field.
- Safe: new optional response fields, new endpoints, new optional request fields with a server-side default, new `code` values on a status the app already treats as a refusal.
- Breaking: renaming/removing a response field the app reads, a nullable becoming null where Kotlin has no default, a new required request field, changing a route, method or success status (`200` → `204` changes `CommandResult.Ok` into `Left`), a new status the app maps to the wrong result.
- For a break: add the new shape next to the old (new route or new optional field), ship the app, raise `Games:MinimumAppVersion` to that release (`the-prey-play-store`; `app-demand-updates` plans a `update:required` PR label and a promote workflow for this, neither exists yet), and only then remove the old shape.
- Removing a Kotlin DTO property is always safe; adding one without a default is safe only if every server version the app can meet sends it.
- Superseded reads stay: `GET /games/{id}`, `/status`, `/state` remain although `/gamestate` replaces them.

## Making a change — order of work

1. Server: DTO in `Abstractions/DataTransferObjects` (`sealed record`), mapper, endpoint `.Produces<…>(status)` metadata and result mapping (see `the-prey-server-feature`). New top-level prefix → gateway route in `http-route-config.bicep` **and** the AppHost YARP routes.
2. Server tests: handler tests in the module's `*.Tests` project; assert the DTO values and the result type that decides the status.
3. Android: `@Serializable` DTO (defaults/nullability per the rules above), Retrofit method in `*Api.kt` with a KDoc listing each status, result mapping in the repository, and the new method in `UnusedGamesApi` for game calls.
4. Android tests: decode a JSON sample **shaped exactly like the server output** (`GameStateDtoTest`, `PlayFieldDtoTest`, `UserDataTest`), include `null`s and extra unknown fields; repository tests per status with a fake API and `Response.error(code, body)` (`GameRepositoryTest`, `GameCommandsRepositoryTest`, `PlayFieldRepositoryTest`, `JoinGameRepositoryTest`).
5. Docs: `docs/api/endpoints.md` — the endpoint table row, request/response JSON, the status (and `code`) table. Run `the-prey-docs-sync` if the change is visible to players.

## Verify

```powershell
dotnet build server/src/the-prey.slnx
dotnet test server/src/Games/HexMaster.ThePrey.Games.Tests/
dotnet test server/src/PlayFields/HexMaster.ThePrey.PlayFields.Tests/
dotnet test server/src/Users/HexMaster.ThePrey.Users.Tests/

# Android (from app/src)
./gradlew lintDebug testDebugUnitTest
```

Compare a live response with the Kotlin DTO: run the stack (`the-prey-local-dev`), open the module's Scalar UI (`MapScalarApiReference`, Development only) or its OpenAPI document (`MapOpenApi`, default `/openapi/v1.json`), and check every property the Kotlin DTO reads, its nullability and its casing. List the routes on both sides to spot drift:

```bash
grep -nE 'Map(Get|Post|Put|Delete)\(' server/src/*/HexMaster.ThePrey.*.Api/Endpoints/*.cs
grep -rnE '@(GET|POST|PUT|DELETE)\(' app/src/app/src/main/java/nl/hexmaster/theprey/data
```

## Checklist

1. Server DTO, endpoint metadata and status mapping changed; enums mapped to strings; camelCase not overridden.
2. Kotlin DTO matches names and nullability; non-essential response fields have defaults; request fields have none.
3. Retrofit method, repository result mapping and `UnusedGamesApi` updated; every status the server can return is mapped.
4. New error `code` → Kotlin constant → English and Dutch string.
5. Tests on both sides, including decoding a real-shaped sample with nulls and unknown fields.
6. `docs/api/endpoints.md` (and `realtime.md` if a payload changed) updated.
7. The oldest installed app still works, or the break is staged behind the version gate.
