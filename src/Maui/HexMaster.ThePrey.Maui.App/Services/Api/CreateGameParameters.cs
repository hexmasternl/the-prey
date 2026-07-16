namespace HexMaster.ThePrey.Maui.App.Services.Api;

/// <summary>
/// The values needed to create a game via <c>POST /games</c>. The three durations are in <b>minutes</b>
/// (sent as-is); the two location intervals are already in <b>seconds</b> — the caller
/// (<c>StartGameViewModel</c>) converts the ping minutes to seconds (× 60) before building this record,
/// so <see cref="IGameApiClient.CreateGameAsync"/> sends them verbatim.
/// </summary>
public sealed record CreateGameParameters(
    Guid PlayfieldId,
    string DisplayName,
    int GameDurationMinutes,
    int HeadstartMinutes,
    int EndgameMinutes,
    int DefaultLocationIntervalSeconds,
    int FinalLocationIntervalSeconds);
