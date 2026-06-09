## 1. Domain Model — PlayerState

- [x] 1.1 Add `PlayerState` enum (`Active`, `Passive`, `Out`, `Tagged`) to the Games domain project
- [x] 1.2 Add `PlayerState State` and `DateTimeOffset? LastLocationAt` properties to the participant entity / Table Storage model
- [x] 1.3 Set default value of `State = Active` when creating a participant record on game start
- [x] 1.4 Write unit tests: participant defaults to Active on creation

## 2. Location Recording — State Update

- [x] 2.1 Update `RecordLocationCommand` handler to set `State = Active` and `LastLocationAt = UtcNow` on the submitting participant after persisting location
- [x] 2.2 Ensure the state update is skipped (no-op) when participant is `Out` or `Tagged`
- [x] 2.3 Publish `participant-status-changed` event via `IGameEventBus` when state transitions from `Passive` → `Active` on location record
- [x] 2.4 Write unit tests: Active→Active no event, Passive→Active event emitted, Out/Tagged state not changed

## 3. PlayerStateMonitor Background Service

- [x] 3.1 Create `PlayerStateMonitor : BackgroundService` in the Games module; runs every 30 seconds
- [x] 3.2 Load all InProgress games and their prey participants from the repository
- [x] 3.3 Apply `Active → Passive` transition for preys with `LastLocationAt < now − 5 min`
- [x] 3.4 Apply `* → Out` transition for preys with `LastLocationAt < now − 7 min` and state not `Out` or `Tagged`
- [x] 3.5 Persist all changed participant records
- [x] 3.6 Publish `participant-status-changed` event per transitioned participant via `IGameEventBus`
- [x] 3.7 Skip participants already in `Out` or `Tagged` state
- [x] 3.8 Register `PlayerStateMonitor` in `GamesModuleRegistration.cs`
- [x] 3.9 Write unit tests: Active→Passive at 5 min boundary, Active→Out at 7 min boundary, Out not re-processed, Tagged not transitioned to Out

## 4. Tag Player — Backend

- [x] 4.1 Add `TagPlayerCommand` sealed record (gameId, callerId, targetParticipantId) to `Features/TagPlayer/`
- [x] 4.2 Implement `TagPlayerCommandHandler`: validate caller is hunter, game is InProgress, target is Prey in Active/Passive state; set `State = Tagged`; publish `participant-status-changed`
- [x] 4.3 Add `POST /games/{gameId}/participants/{participantId}/tag` Minimal API endpoint in the Games API project; require authorization; map to `TagPlayerCommand`; return 204 on success
- [x] 4.4 Return 403 when caller is not the hunter; 404 when participant not found; 409 when target state is Out/Tagged or game is not InProgress
- [x] 4.5 Register `TagPlayerCommandHandler` in `GamesModuleRegistration.cs`
- [x] 4.6 Add OTel instrumentation to `TagPlayerCommandHandler` (activity + tags)
- [x] 4.7 Write unit tests for all TagPlayer scenarios (success, 403, 404, 409 variants)

## 5. SSE Event — participant-status-changed

- [x] 5.1 Add `ParticipantStatusChangedEvent` record to the game event bus types
- [x] 5.2 Update SSE stream endpoint to listen for `participant-status-changed` events and broadcast them to all connected participants of the game
- [x] 5.3 Write unit / integration tests: event delivered to all connected clients on state change

## 6. GameStatusDto — State Field

- [x] 6.1 Add `State` (string) property to `ParticipantSnapshotDto` in `Abstractions/DataTransferObjects/`
- [x] 6.2 Update `GetGameStatusQueryHandler` to populate `State` from participant's `PlayerState`
- [x] 6.3 Update `PreysLeft` computation in the handler to count only `Active` + `Passive` participants
- [x] 6.4 Set hunter participant's `State` to `"Active"` always in the handler
- [x] 6.5 Update `participant-located` SSE event payload to include `participantState` field
- [x] 6.6 Write unit tests: PreysLeft count excludes Tagged/Out, State field populated correctly

## 7. Frontend — Hunter View Updates

- [x] 7.1 Update `GameHunterPage` to read `State` from `ParticipantSnapshotDto` and local SSE state map
- [x] 7.2 Render Active/Passive prey blips in hunter-red flashing style; Tagged/Out blips in grey non-flashing style
- [x] 7.3 Subscribe to `participant-status-changed` SSE events; update local participant state map and re-render affected blip
- [x] 7.4 Update preys-remaining HUD counter to count Active + Passive only; recalculate on `participant-status-changed`
- [x] 7.5 Update nearest-prey distance to use only Active/Passive preys with known positions
- [x] 7.6 Add "Tag Player" button to the hunter HUD panel
- [x] 7.7 Implement tag-player modal/sheet: fetch current status on open, display Active+Passive preys only, confirm before calling API
- [x] 7.8 Call `POST /games/{gameId}/participants/{participantId}/tag` on confirm; disable button while in flight; dismiss modal on 204
- [x] 7.9 Handle `participant-located` SSE event: update `participantState` in local state from event payload

## 8. Frontend — Prey View Updates

- [x] 8.1 Update `GamePreyPage` to read `State` from `ParticipantSnapshotDto` and local SSE state map
- [x] 8.2 Subscribe to `participant-status-changed` SSE events; update local participant state map
- [x] 8.3 Update preys-remaining HUD counter to count Active + Passive only; recalculate on `participant-status-changed`
- [x] 8.4 Detect when own-player `participant-status-changed` arrives with `newState: "Tagged"` → show "You have been tagged. Game over for you.", stop polling, close SSE
- [x] 8.5 Detect when own-player `participant-status-changed` arrives with `newState: "Out"` → show "You left the area for too long. You are out.", stop polling, close SSE

## 9. Integration & QA

- [x] 9.1 Verify end-to-end: prey goes silent 5 min → hunter SSE receives participant-status-changed Passive; prey broadcasts → Active event
- [x] 9.2 Verify end-to-end: prey silent 7 min → Out event; prey view shows out message
- [x] 9.3 Verify end-to-end: hunter taps Tag Player, confirms, prey SSE receives Tagged, prey view shows tagged message
- [x] 9.4 Verify preys-remaining count correct in hunter and prey HUDs after mixed state transitions
- [x] 9.5 Run full test suite: `dotnet test src/Games/HexMaster.ThePrey.Games.Tests/`
