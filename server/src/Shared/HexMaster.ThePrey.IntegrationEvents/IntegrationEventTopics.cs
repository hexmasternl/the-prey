namespace HexMaster.ThePrey.IntegrationEvents;

/// <summary>
/// The canonical pub/sub topic names. Producers (the Games sweep) and the Notifications consumer
/// share these constants so the contract cannot drift. One topic per event type, kebab-case.
/// </summary>
public static class IntegrationEventTopics
{
    // Envelope topics: carry an in-process game/lobby event (its name + payload) across the service
    // boundary so the Notifications module can fan it out to clients verbatim.
    public const string GameNotification = "game-notification";
    public const string LobbyNotification = "lobby-notification";
}
