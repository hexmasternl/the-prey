## Why

The game currently tracks participant GPS locations but has no lifecycle state for individual preys — there is no way for the system to distinguish a prey who is actively hiding from one who has gone silent, quit, or been tagged. This change introduces a `PlayerState` enum that drives game progression, enables the hunter to formally tag a prey, and surfaces per-player state in all real-time client views.

## What Changes

- Add `PlayerState` enum to the Games domain: `Active`, `Passive`, `Out`, `Tagged`
- Default all preys to `Active` on game start
- System automatically transitions `Active` → `Passive` when a prey has not broadcast GPS in the last 5 minutes
- System automatically transitions `Passive` → `Active` when a prey broadcasts GPS again
- System automatically transitions `Active` or `Passive` → `Out` when a prey has not broadcast GPS in the last 7 minutes (irreversible)
- Add "Tag Player" button to the hunter HUD; clicking it lists active/passive preys; confirming calls a new API endpoint that transitions the selected prey to `Tagged` (irreversible)
- **BREAKING**: `Participants` array in `GameStatusDto` gains a `State` field (`Active`, `Passive`, `Tagged`, `Out`)
- **BREAKING**: `participant-located` SSE event gains a `participantState` field
- New SSE event `participant-status-changed` emitted to all connected clients when a prey's state changes
- HUD "preys remaining" counter on both hunter and prey views now shows count of participants in `Active` or `Passive` state
- Participants in `Tagged` or `Out` state render as grey dots on all maps; `Active`/`Passive` preys render in normal colour

## Capabilities

### New Capabilities

- `player-status-tracking`: State machine for individual prey lifecycle (Active/Passive/Out/Tagged), including automatic timeout transitions driven by GPS broadcast timestamps
- `tag-player-action`: Hunter UI and server endpoint for the hunter to mark a specific Active or Passive prey as Tagged

### Modified Capabilities

- `game-status-endpoint`: `GameStatusDto.Participants` entries must carry `State`; active-prey count must reflect only `Active`+`Passive` participants
- `game-stream-endpoint`: New `participant-status-changed` SSE event; `participant-located` event gains `participantState`
- `hunter-view`: Add Tag Player button to HUD; render Tagged/Out prey blips as grey; preys-remaining count uses `Active`+`Passive` filter
- `prey-view`: Preys-remaining HUD count uses `Active`+`Passive` filter; own state communicated to player (grey overlay when `Passive`, game-over message when `Out` or `Tagged`)

## Impact

- **Backend**: Games module gains `PlayerState` enum, a `TagPlayerCommand` handler, an automatic state-machine job/hook triggered on location updates and on a background timer, and a new `POST /games/{gameId}/tag-player` endpoint
- **Backend**: `GameStatusDto` and its query handler updated; `IGameEventBus` publishes new event type
- **Frontend**: `GameHunterPage` and `GamePreyPage` updated; new tag-player modal/sheet in hunter view
- **Data**: Game participant records in Table Storage gain a `State` column (default `Active`)
