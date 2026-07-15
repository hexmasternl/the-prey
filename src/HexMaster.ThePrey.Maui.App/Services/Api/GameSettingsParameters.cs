namespace HexMaster.ThePrey.Maui.App.Services.Api;

/// <summary>
/// The five tuning values the owner is editing, all expressed in the units the UI presents:
/// every value is in <b>minutes</b>. <see cref="IGameApiClient.UpdateGameSettingsAsync"/> converts the
/// two ping intervals to seconds (× 60) when building the request; the three durations are sent as-is.
/// </summary>
public sealed record GameSettingsParameters(
    int GameDurationMinutes,
    int HeadstartMinutes,
    int EndgameMinutes,
    int PingMinutes,
    int EndgamePingMinutes);
