# Low — Low-entropy join code with no attempt lockout

| | |
|---|---|
| **Severity** | Low |
| **Category** | Abuse / brute force |
| **Component** | Games — CreateGame / JoinGame |
| **Status** | Open |

## Summary

Game join codes are 4-digit numeric (10,000 possibilities). Joining requires both the game GUID *and* the code, which substantially mitigates the risk, but combined with the absence of rate limiting, an attacker who knows or guesses a game GUID can brute-force all codes quickly.

## Evidence

- `src/Games/HexMaster.ThePrey.Games/Features/CreateGame/CreateGameCommandHandler.cs:93-98` — 4-digit code, generated with a cryptographic RNG (`RandomNumberGenerator` — good) but only a 10,000 space.
- `src/Games/HexMaster.ThePrey.Games/Features/JoinGame/JoinGameCommandHandler.cs:33` — match requires the code on the route `/games/{id:guid}/join`.
- No rate limiting exists (see [medium-no-rate-limiting](./medium-no-rate-limiting.md)).

## Impact

For a known game id, all 10,000 codes can be tried in seconds without throttling, allowing unauthorized lobby joins. The required GUID raises the bar (it is not enumerable), so the practical risk is Low.

## Recommendation

1. Add per-game and per-caller attempt throttling/lockout on the join endpoint.
2. Consider a longer alphanumeric code to increase entropy.
3. Expire/rotate codes once the game starts.
