namespace HexMaster.ThePrey.Maui.App.Services.Realtime;

/// <summary>
/// The real-time event <c>type</c> strings the game channel emits (matching the backend / Ionic client
/// event names). Lobby events in <see cref="FullSnapshotEvents"/> carry a complete game payload and
/// replace the local state wholesale; the typed in-game events below mutate one slice of it.
/// </summary>
public static class GameRealtimeEventTypes
{
    public const string StateChanged = "state-changed";
    public const string PlayerLocationUpdated = "player-location-updated";
    public const string ParticipantStatusChanged = "participant-status-changed";
    public const string PlayerPenalized = "player-penalized";
    public const string GameEnded = "game-ended";

    /// <summary>
    /// Events whose payload is a complete game snapshot (the lobby events and <c>game-started</c>).
    /// Receiving any of these replaces the authoritative local state with the payload.
    /// </summary>
    public static readonly IReadOnlySet<string> FullSnapshotEvents = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "lobby-updated",
        "settings-updated",
        "ready-updated",
        "hunter-designated",
        "hunter-changed",
        "game-started",
    };
}
