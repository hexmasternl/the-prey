namespace HexMaster.ThePrey.Maui.App.Services.Api;

/// <summary>
/// Client-side projection of the backend <c>GameDto</c> — only the fields the lobby renders. Shapes
/// deserialize case-insensitively from the backend's camelCase JSON (both the <c>GET /games/{id}</c>
/// body and every lobby-stream snapshot), so extra backend fields are simply ignored.
/// </summary>
public sealed record GameDetails(
    Guid Id,
    string GameCode,
    string Status,
    GameConfigurationDetails Configuration,
    IReadOnlyList<GameParticipantDetails> Participants,
    Guid? HunterUserId,
    Guid OwnerUserId,
    bool IsOwnerPlayer,
    bool IsReadyToStart)
{
    /// <summary>True while the game is still in its lobby phase (settings + readiness editable).</summary>
    public bool IsLobby => string.Equals(Status, "Lobby", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// The five tuning values the lobby shows. Durations are in minutes; the two location intervals are
/// in seconds (converted to minutes for display, back to seconds on save) — the backend convention.
/// </summary>
public sealed record GameConfigurationDetails(
    int GameDuration,
    int HunterDelayTime,
    int FinalStageDuration,
    int DefaultLocationInterval,
    int FinalLocationInterval);

/// <summary>One participant as the lobby lists it: name, ready flag, and lobby/game state.</summary>
public sealed record GameParticipantDetails(
    Guid UserId,
    string DisplayName,
    bool IsReady,
    string State);
