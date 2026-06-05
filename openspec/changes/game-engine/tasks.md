## 1. Infrastructure & Project Setup

- [ ] 1.1 Add Azure Storage Queue resource to the Aspire AppHost (`game-engine-queue`)
- [ ] 1.2 Add Azure Container Apps Job resource to the Aspire AppHost, wired to the queue trigger
- [ ] 1.3 Create the `HexMaster.ThePrey.GameEngine` project (Worker Service / Console app) with Aspire service defaults
- [ ] 1.4 Add NuGet references: `Azure.Storage.Queues`, `Npgsql.EntityFrameworkCore.PostgreSQL`, `OpenTelemetry.Extensions.Hosting`
- [ ] 1.5 Register the game engine project in `the-prey.slnx`

## 2. Queue Trigger & Startup

- [ ] 2.1 Implement queue message deserialization (extract `gameId` from the message body)
- [ ] 2.2 Implement startup validation: reject (dead-letter) messages with absent, empty, or unknown `gameId`
- [ ] 2.3 Implement startup validation: reject messages for games that are not in the InProgress state
- [ ] 2.4 Load the full game record (participants, location histories, penalties) from PostgreSQL on startup
- [ ] 2.5 Write the `GameEngineActivitySource` and OpenTelemetry instrumentation for the startup phase

## 3. GameLocationChecker Scheduling

- [ ] 3.1 Implement tick alignment logic: compute the next `StartTime + (N × 30s)` boundary from the current time
- [ ] 3.2 Implement the repeating timer that fires the `GameLocationChecker` at each aligned boundary
- [ ] 3.3 Ensure the timer does not drift — each tick is anchored to `StartTime`, not to previous tick completion
- [ ] 3.4 Implement game-end detection: poll game status from PostgreSQL at the start of each cycle

## 4. GameLocationChecker — Broadcast Eligibility

- [ ] 4.1 Implement per-participant eligibility evaluation: penalty override (any active penalty → include)
- [ ] 4.2 Implement per-participant eligibility evaluation: final-stage interval (`FinalLocationInterval`)
- [ ] 4.3 Implement per-participant eligibility evaluation: default interval (`DefaultLocationInterval`)
- [ ] 4.4 Track last-broadcast timestamp per participant in memory (reset from game state on engine start)

## 5. GameLocationChecker — Location Read & Update

- [ ] 5.1 For each eligible participant, query their location history from PostgreSQL and select the most recent entry
- [ ] 5.2 Skip participants with an empty location history (no coordinate available)
- [ ] 5.3 Write the most recent coordinate to `GameParticipant.Location` in PostgreSQL for each eligible participant
- [ ] 5.4 Build the `POST /game-engine/{gameId}/location-update` request payload (array of `{ UserId, GpsLocation }`)
- [ ] 5.5 Skip the HTTP call if no participants are eligible in the current cycle

## 6. Final Broadcast & Clean Exit

- [ ] 6.1 Implement the final broadcast: read last known location for all participants regardless of eligibility
- [ ] 6.2 Update all participants' `Location` properties in PostgreSQL before the final broadcast call
- [ ] 6.3 Call `POST /game-engine/{gameId}/location-update` with the full participant list on game end
- [ ] 6.4 Ensure the Container Apps Job exits with exit code 0 after a clean shutdown

## 7. Games API — location-update Endpoint

- [ ] 7.1 Create the `game-engine` endpoint group in the Games API (`/game-engine/{gameId}/...`)
- [ ] 7.2 Implement `POST /game-engine/{gameId}/location-update` — deserialize and validate the payload
- [ ] 7.3 Validate `gameId` exists and is InProgress; return 404 / 422 on failure
- [ ] 7.4 Implement `X-Engine-Key` header validation (shared secret from environment variable); return 401 on failure
- [ ] 7.5 Silently ignore payload entries whose `UserId` does not match a game participant

## 8. Games API — SSE Stream

- [ ] 8.1 Implement `GET /game-engine/{gameId}/stream` SSE endpoint with `Content-Type: text/event-stream`
- [ ] 8.2 Implement in-memory SSE connection registry keyed by `gameId` (holds active response streams)
- [ ] 8.3 Register the SSE connection on client connect; remove it on disconnect or cancellation
- [ ] 8.4 On a successful location-update request, emit one SSE event per eligible participant to all registered connections for the game
- [ ] 8.5 Return 404 for SSE stream requests where the `gameId` does not exist
- [ ] 8.6 Require user JWT authentication on the SSE stream endpoint (`.RequireAuthorization()`)

## 9. Games Domain — GameParticipant.Location Clarification

- [ ] 9.1 Confirm `GameParticipant.Location` column exists in the PostgreSQL schema; add EF Core migration if missing
- [ ] 9.2 Update the `RecordPlayerLocation` command handler to NOT update `GameParticipant.Location` (location history only)
- [ ] 9.3 Update the Games API spec delta: verify the modified `Recording player locations` requirement is reflected in the implementation

## 10. StartGame — Enqueue Trigger

- [ ] 10.1 Inject the Azure Storage Queue client into the `StartGame` command handler
- [ ] 10.2 After persisting the InProgress game state, enqueue a message with the `gameId`
- [ ] 10.3 If enqueuing fails, surface the error to the caller and do not transition the game to InProgress

## 11. Observability

- [ ] 11.1 Add OTel activity spans for each GameLocationChecker cycle (start, eligibility evaluation, DB update, HTTP call)
- [ ] 11.2 Add OTel activity spans for the final broadcast phase
- [ ] 11.3 Add a counter metric: number of locations broadcasted per cycle
- [ ] 11.4 Add a counter metric: number of cycles executed per game

## 12. Unit Tests

- [ ] 12.1 Test tick alignment logic for on-time and late-starting engine scenarios
- [ ] 12.2 Test eligibility evaluation: penalty override takes precedence over all interval rules
- [ ] 12.3 Test eligibility evaluation: final-stage interval applied when game is in end-game status
- [ ] 12.4 Test eligibility evaluation: default interval applied outside the final stage
- [ ] 12.5 Test eligibility evaluation: participant not yet due is excluded
- [ ] 12.6 Test that a participant with no location history is excluded from the broadcast payload
- [ ] 12.7 Test `X-Engine-Key` middleware: missing key returns 401, correct key passes through
- [ ] 12.8 Test `location-update` endpoint: unknown gameId returns 404, non-InProgress returns 422
- [ ] 12.9 Test `location-update` endpoint: non-participant UserId entries are silently ignored
