## Why

Games currently lack lifecycle timestamps and the background cleanup service uses a poorly-defined "ended 24 hours ago" heuristic that doesn't account for games that are never started or abandoned in the lobby. Introducing explicit `CreatedAt`, `EndsAt`, and `CleanUpAfter` fields gives the domain model a clear, deterministic lifecycle and enables the cleanup service to make precise, auditable removal decisions based on a server-assigned deadline.

## What Changes

- Add `CreatedAt` (`DateTimeOffset`) to the `Game` aggregate — set by the server when the game is first created.
- Add `EndsAt` (`DateTimeOffset?`) to the `Game` aggregate — calculated as `StartedAt + GameDuration` and set when the game transitions to `InProgress`.
- Add `CleanUpAfter` (`DateTimeOffset`) to the `Game` aggregate — set at creation time as `CreatedAt + 48 hours`.
- Update the background cleanup service to delete games where `CleanUpAfter <= now` (replacing the previous "ended 24 hours ago" logic).
- Add the three columns to the PostgreSQL `Games` table via an EF Core migration.
- Expose `CreatedAt`, `EndsAt`, and `CleanUpAfter` in `GameDto` so clients can display or act on lifecycle information.

## Capabilities

### New Capabilities

- `game-lifecycle-timestamps`: Domain model fields and persistence for `CreatedAt`, `EndsAt`, and `CleanUpAfter` on the `Game` aggregate.
- `game-cleanup-service`: Background service that periodically removes games whose `CleanUpAfter` timestamp has passed.

### Modified Capabilities

- `games`: The `Game` aggregate gains three new `DateTimeOffset` fields (`CreatedAt`, `EndsAt`, `CleanUpAfter`) that are set at defined lifecycle transitions (creation and game start). `GameDto` is extended to expose these fields to callers.

## Impact

- **Domain model**: `Game.cs` gains three new properties; factory/start methods updated.
- **Data adapter**: New EF Core migration adds `CreatedAt`, `EndsAt`, `CleanUpAfter` columns to the `Games` table; entity mapping updated.
- **DTOs**: `GameDto` gains `CreatedAt`, `EndsAt`, and `CleanUpAfter` fields.
- **Background service**: Cleanup query changes from `EndedAt <= now - 24h` to `CleanUpAfter <= now`.
- **No breaking API changes** — the new DTO fields are additive.
