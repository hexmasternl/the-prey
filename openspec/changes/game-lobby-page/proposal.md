## Why

Players who create a game have no dedicated UI to manage the pre-game lobby: invite others, adjust settings, designate roles, and confirm everyone is ready. This change delivers the full lobby experience — from game-code sharing to real-time participant updates — so a game can actually reach the "Start" button.

## What Changes

- A new `GameLobbyPage` Ionic modal/page that all participants see once a game is in the Lobby state
- A short, human-readable **game code** is generated server-side when a game is created and exposed in the game DTO so players can share it out-of-band to invite others
- Each lobby participant gains an `IsReady` flag; changing any game setting by the owner resets all non-owner ready flags to `false`; the owner is implicitly always ready
- A "Ready" button for non-owner participants; disabled once clicked; re-enabled whenever the owner changes a setting
- Game owner can remove a participant from the lobby via a swipe-to-delete action
- Game owner can tap a participant row to designate them as hunter (all others become prey roles for display only — actual role assignment still happens at game start)
- Game owner can edit game configuration settings; changes are pushed to the server via `PUT /games/{id}/settings`
- A new `DELETE /games/{id}/lobby/{userId}` endpoint to remove a participant
- A new `PUT /games/{id}/settings` endpoint to update game configuration
- A new SSE endpoint `GET /games/{id}/lobby/stream` that pushes lobby state changes to all connected participants in real time

## Capabilities

### New Capabilities

- `game-lobby-ui`: The Ionic frontend page — game code display, settings panel, participant list with roles and ready indicators, ready button, swipe-to-delete, real-time SSE updates
- `game-code`: Short alphanumeric code generated at game creation and included in all game responses; used to invite players
- `lobby-ready-status`: Per-participant `IsReady` flag; automatically reset when owner changes settings; exposed in game responses; owner is exempt from the ready requirement
- `lobby-sse-stream`: Server-Sent Events endpoint that streams lobby state changes (participant joined/removed, settings changed, ready state toggled) to all lobby participants
- `lobby-player-management`: `DELETE /games/{id}/lobby/{userId}` to remove a participant (owner only); `PUT /games/{id}/settings` to update game configuration (owner only)

### Modified Capabilities

- `games`: Game creation response and `GameDto` gain a `GameCode` field; lobby participants gain `IsReady` and `Role` fields visible from the Lobby state

## Impact

- Backend: `Game` and `GameParticipant` domain models gain new fields; new endpoints and a handler each for remove-participant, update-settings, and SSE stream; `GameDto` and `GameSummaryDto` updated
- Frontend: new `GameLobbyPage` component; `GamesService` gains methods for the three new endpoints plus SSE subscription; home page "Playing" button can route to this page when the game is in Lobby state
- Database: EF Core migration adds `GameCode` column to games table and `IsReady` column to participants table
