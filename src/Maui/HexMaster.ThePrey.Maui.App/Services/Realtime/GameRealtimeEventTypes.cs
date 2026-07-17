namespace HexMaster.ThePrey.Maui.App.Services.Realtime;

/// <summary>
/// The real-time event <c>type</c> strings the game channel emits — the canonical catalog mirrored from
/// <c>HexMaster.ThePrey.IntegrationEvents.RealtimeProtocol.MessageTypes</c> (see <c>docs/api/realtime.md</c>).
/// There are no more full-snapshot events: every lobby message is a delta that mutates one slice of the
/// locally-held state (participant collection or game-level configuration), and every gameplay message
/// mutates the corresponding slice (locations, a prey's state/penalty, or the terminal game-ended).
/// </summary>
public static class GameRealtimeEventTypes
{
    // Lobby deltas — never full-game snapshots.

    /// <summary><c>data</c> = full participant. A participant entered the lobby.</summary>
    public const string ParticipantJoined = "participant-joined";

    /// <summary><c>data</c> = full participant. A participant's ready flag, callsign, state, or penalty changed.</summary>
    public const string ParticipantChanged = "participant-changed";

    /// <summary><c>data</c> = <c>{ userId }</c>. A participant left or was removed.</summary>
    public const string ParticipantRemoved = "participant-removed";

    /// <summary>
    /// <c>data</c> = the game-level slice (status + configuration + hunter/timing; no participants, no
    /// per-caller flags). The single carrier for every status transition.
    /// </summary>
    public const string ConfigurationChanged = "configuration-changed";

    // Gameplay.

    /// <summary><c>data</c> = <c>{ locations: [ { userId, role, latitude, longitude, state } ] }</c> — one or more, batched.</summary>
    public const string LocationsUpdated = "locations-updated";

    /// <summary><c>data</c> = <c>{ userId, event, state?, penaltyEndsAt?, reason? }</c>. A prey was tagged or (un)penalized.</summary>
    public const string PreyUpdated = "prey-updated";

    /// <summary><c>data</c> = <c>{ outcome, survivorCount, completedAt }</c>. Emitted exactly once per game.</summary>
    public const string GameEnded = "game-ended";

    // Control.

    /// <summary><c>data</c> = <c>{ reason }</c>. Server hint telling clients to pull a fresh full snapshot.</summary>
    public const string ResyncRequested = "resync-requested";

    /// <summary>The <c>event</c> discriminator values carried by a <see cref="PreyUpdated"/> payload.</summary>
    public static class PreyEvents
    {
        public const string Tagged = "tagged";
        public const string Penalized = "penalized";
        public const string PenaltyCleared = "penalty-cleared";
    }
}
