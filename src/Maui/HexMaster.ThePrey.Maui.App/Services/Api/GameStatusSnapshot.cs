namespace HexMaster.ThePrey.Maui.App.Services.Api;

/// <summary>
/// Client-side projection of the backend <c>GameStatusDto</c> returned by <c>GET /games/{id}/status</c>
/// — only the fields the in-game HUD renders. Deserializes case-insensitively from the backend's
/// camelCase JSON, so the richer backend payload's extra fields are simply ignored. The HUD seeds its
/// countdown bars from <see cref="NextPingDuration"/> / <see cref="GameDurationLeft"/> and ticks them
/// down locally between polls.
/// </summary>
public sealed record GameStatusSnapshot(
    int GameDurationLeft,
    int NextPingDuration,
    int CurrentPingInterval,
    bool IsEndgame,
    int PreysLeft,
    Guid? HunterUserId,
    IReadOnlyList<GameParticipantSnapshot> Participants);

/// <summary>The minimal participant projection the HUD needs — enough to count the preys in the game.</summary>
public sealed record GameParticipantSnapshot(Guid UserId);
