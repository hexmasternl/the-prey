## Why

The game is currently capable of recording player locations, but nothing drives the broadcasting of those locations to participants during an active game session. The game engine is the missing runtime component that orchestrates location broadcasting, ensuring players see each other's positions on the correct schedule throughout the game.

## What Changes

- A new **Azure Container Apps Job** is introduced as the game engine — triggered by an Azure Storage Queue message containing the `gameId`.
- The game engine downloads the full game state from PostgreSQL on startup and remains running for the lifetime of the game.
- A **GameLocationChecker** function runs every 30 seconds (aligned to the game start time, not the job start time) and determines which players' locations to broadcast.
- Broadcasting rules are applied per-player each cycle: default interval, end-game (final stage) interval, and penalty override (every cycle if active penalty).
- A new internal API endpoint **`POST /game-engine/{gameId}/location-update`** is added to the Games API. It accepts an array of `{ UserId, GpsLocation }` and broadcasts each location to all game participants via **Server-Sent Events (SSE)**.
- When the game ends, a final location broadcast is performed for all players before the Container Apps Job exits.
- Player devices continue to POST locations to the existing location-recording endpoint — this does **not** change. Those submissions only update location history; they do not trigger broadcasting.

## Capabilities

### New Capabilities

- `game-engine-job`: Azure Container Apps Job that receives a `gameId` via Azure Storage Queue message, loads the game from PostgreSQL, schedules the GameLocationChecker every 30 seconds aligned to game start, performs a final broadcast on game end, and exits cleanly.
- `game-location-checker`: The periodic function (invoked by the game engine every 30 seconds) that evaluates each participant's broadcast eligibility based on the default interval, final-stage interval, and active-penalty override. For eligible players it reads the last known location from their location history and updates `GameParticipant.Location`, then calls the location-update API endpoint.
- `game-engine-location-update`: The new `POST /game-engine/{gameId}/location-update` endpoint in the Games API that accepts an array of `{ UserId, GpsLocation }` pairs and broadcasts each player's location to all game session participants over SSE.

### Modified Capabilities

- `games`: The `GameParticipant` model gains a writable `Location` property (last broadcasted GPS position), updated by the game engine each broadcast cycle. The existing location-recording endpoint behaviour is unchanged — it still appends to history and does not broadcast.

## Impact

- **New project**: `HexMaster.ThePrey.GameEngine` — a .NET Worker Service / Console app deployable as an Azure Container Apps Job.
- **Games API**: New `game-engine` endpoint group (`/game-engine/{gameId}/location-update`). New SSE infrastructure wired into the Games API project.
- **Azure infrastructure**: Azure Storage Queue for game-engine trigger messages; Azure Container Apps Job definition (new resource in the Aspire AppHost).
- **PostgreSQL**: No schema changes beyond the `Location` column on `GameParticipant` (which may already exist from the games spec).
- **No changes** to the player location-submission endpoint or the domain rules around location history.
