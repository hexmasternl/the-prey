# Medium — Server trusts client-reported location and timestamps (spoofing / cheating)

| | |
|---|---|
| **Severity** | Medium |
| **Category** | Game integrity / input validation |
| **Component** | Games — RecordPlayerLocation |
| **Status** | Open |

## Summary

The server accepts client-supplied GPS coordinates, accuracy, and **timestamp** largely on trust. Coordinates are validated only against valid WGS84 ranges; there are no plausibility checks (speed/teleport), no playfield-containment validation, and the client-supplied `RecordedAt` is accepted without bounds — including for security-relevant timing such as the hunter head-start penalty.

## Evidence

- `src/Games/HexMaster.ThePrey.Games/Features/RecordPlayerLocation/RecordPlayerLocationCommandHandler.cs:42` — `var recordedAt = command.RecordedAt ?? now;` (client timestamp used verbatim when supplied).
- `RecordLocationRequest` DTO (`Requests.cs:29-33`) — `RecordedAt` and `Accuracy` are client-supplied, nullable, and unvalidated.
- `GpsCoordinate.Create` validates only lat ∈ [-90,90], lon ∈ [-180,180] — no containment or motion plausibility.
- Tagging proximity (`Game.cs:401-409`) and the hunter head-start penalty (`AssessHunterHeadStartPenalty`, `Game.cs:347-349`) consume these stored, client-influenced positions/timestamps.

## Impact

A modified or scripted client can:

- **teleport** to evade the hunter or to fake being within `TagRangeMeters` and tag a prey illegitimately;
- **backdate `RecordedAt`** to dodge the head-start movement penalty or otherwise manipulate timing-based rules;
- submit zero/implausible `Accuracy` to appear precisely located.

This is primarily a game-integrity/cheating concern; it does not by itself expose other players' data, but it undermines fair play and the penalty system.

## Recommendation

1. **Bound `RecordedAt`** to a small window around server time (e.g. ±60s); otherwise use server time. Prefer server time for any security/penalty-relevant timing.
2. **Validate `Accuracy`** (`>= 0`, reject absurd values) and consider rejecting readings with poor accuracy for tagging decisions.
3. Add **speed/teleport plausibility** checks between consecutive readings per player.
4. Enforce **playfield containment** server-side for boundary penalties rather than trusting the client's "in bounds" state (pairs with spatial indexing in the improvements backlog).
5. Combine with rate limiting ([medium-no-rate-limiting](./medium-no-rate-limiting.md)).
