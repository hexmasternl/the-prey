## Why

The Prey is a location-based pursuit game, but there is currently no concept of a **game**: nothing ties a play field, an organizing player, a set of participants, and the timing/penalty rules of a round together. Without a Game domain model there is nowhere to host a lobby, designate the hunter and the preys, capture the GPS location stream each player sends, or enforce the rules (head-start delay, final-stage acceleration, boundary penalties) that make a round playable. This change introduces that aggregate so every later gameplay feature has a single, rule-enforcing home.

## What Changes

- Introduce a new **Games** domain module (`HexMaster.ThePrey.Games`) following the modular-monolith / feature-slice conventions (ADR 0002, ADR 0009), populating the existing empty `/Games/` solution folder.
- Add a `Game` **aggregate root** that owns its lobby, its single hunter, its collection of preys, and its configuration, and that enforces all game invariants through behavior (no public setters).
- Add a `GameConfiguration` value object holding the tunables: `GameDuration` (minutes), `HunterDelayTime` (minutes — the head-start before hunters may move), `FinalStageDuration` (minutes, must be smaller than `GameDuration`), `DefaultLocationInterval` and `FinalLocationInterval` (seconds), and the two boundary-penalty toggles — with interdependent validation.
- Add a `LobbyPlayer` value object (`UserId`, `DisplayName`, optional profile picture) and a lobby collection on the game.
- Model the hunter and the preys as a single in-aggregate `GameParticipant` entity carrying a role, a nullable current `Location`, a collection of `Penalty` records, and a collection of `LocationReading` history entries. The game exposes exactly one `Hunter` and a collection of `Preys`, both of whose user ids must reference lobby members.
- Add a `GpsCoordinate` value object (latitude/longitude with range validation) local to the module.
- Implement the core game rules in the domain: a player with an **active penalty** reports its location every 10 seconds; the **final stage** (the last `FinalStageDuration` minutes) switches reporting to `FinalLocationInterval`; hunters may only move after `HunterDelayTime`.
- Add API endpoints (Minimal APIs + CQRS handlers) to **create a game**, **join its lobby**, **start the game** (designate the hunter, turn the rest of the lobby into preys), **record a player location**, **retrieve a game**, and **list the games visible to the caller**.
- Persist games in **PostgreSQL** via Entity Framework Core and the Aspire PostgreSQL hosting + Npgsql client integrations, in a dedicated data adapter project (`HexMaster.ThePrey.Games.Data.Postgres`) with EF Core migrations.
- Wire a PostgreSQL resource and database into the Aspire AppHost and reference it from the Games API.
- Add observability (ActivitySource + metrics) and a Tests project mirroring the feature slices.

## Capabilities

### New Capabilities
- `games`: Defining, configuring, and running a game round — the `Game` aggregate, its lobby, the hunter/prey designation, the GPS location stream and history, the penalty and final-stage timing rules, the create/join/start/record/get/list use cases, and EF Core + PostgreSQL persistence.

### Modified Capabilities
<!-- None — this is a greenfield capability. -->

## Impact

- **New projects** (under `src/Games/`): `HexMaster.ThePrey.Games`, `HexMaster.ThePrey.Games.Abstractions`, `HexMaster.ThePrey.Games.Api`, `HexMaster.ThePrey.Games.Data.Postgres`, `HexMaster.ThePrey.Games.Tests`.
- **Modified projects**: `ThePrey.Aspire.AppHost/AppHost.cs` (add PostgreSQL resource + database, add and reference the Games API project); `ThePrey.Aspire.ServiceDefaults/AspireConstants.cs` (add Games API, Postgres server, and Games database resource/connection names); `src/the-prey.slnx` and `HexMaster.ThePrey.slnx` (register the new projects under the `/Games/` folder).
- **New dependencies**: `Aspire.Hosting.PostgreSQL` (AppHost); `Aspire.Npgsql.EntityFrameworkCore.PostgreSQL`, `Microsoft.EntityFrameworkCore.Design` (Data adapter / API).
- **APIs**: New `/games` endpoint group — create, join lobby, start, record location, get-by-id, list. Authenticated; the caller's `sub` claim (parsed to a `Guid`) identifies the user.
- **Scope**: Backend only — no MAUI app / frontend changes. Automatic boundary-penalty *detection* (which requires the `PlayField` geometry from the PlayFields module) is out of scope; this change models the penalty data and timing rules and exposes the operations a future orchestration feature will call.
