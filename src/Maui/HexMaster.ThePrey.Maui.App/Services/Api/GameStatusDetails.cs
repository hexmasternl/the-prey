namespace HexMaster.ThePrey.Maui.App.Services.Api;

/// <summary>
/// Rich client-side projection of the backend <c>GameStatusDto</c> returned by
/// <c>GET /games/{id}/status</c> — the fields the full-screen gameplay map renders: the playfield
/// polygon, every participant's last-known location and state, the hunter's id, and the head-start
/// moment. Distinct from the minimal <see cref="GameStatusSnapshot"/> (which the compact HUD uses):
/// this one carries the map geometry and per-player positions. Deserializes case-insensitively from
/// the backend's camelCase JSON, so the richer payload's extra fields are simply ignored.
/// </summary>
public sealed record GameStatusDetails(
    IReadOnlyList<GpsCoordinate> PlayfieldCoordinates,
    IReadOnlyList<GameParticipantStatusDetails> Participants,
    Guid? HunterUserId,
    int GameDurationLeft,
    DateTimeOffset? HunterMayMoveAt,
    bool IsEndgame,
    int PreysLeft,
    // The next-ping countdown seed and its full interval — the compact HUD's ping bar reads these off the
    // same /status payload the map already fetches. Trailing + defaulted so existing constructions still bind.
    int NextPingDuration = 0,
    int CurrentPingInterval = 0,
    // Seconds until the next sweep tick for a penalised participant, clamped to [0, 30]; 0 when not
    // penalised. Seeds the HUD's fixed-30-second penalty countdown bar.
    int NextPingDurationWithPenalty = 0);

/// <summary>
/// One participant as the gameplay map plots it: the <see cref="UserId"/> (matched against the caller
/// and the hunter to pick the blip color), the <see cref="LastKnownLocation"/> (a dot is drawn only
/// when this is non-<c>null</c>), and the <see cref="State"/> (<c>Active</c>/<c>Passive</c> vs
/// <c>Tagged</c>/<c>Out</c>) that greys a caught prey.
/// </summary>
public sealed record GameParticipantStatusDetails(
    Guid UserId,
    GpsCoordinate? LastKnownLocation,
    string State);
