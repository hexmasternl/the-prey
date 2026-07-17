namespace HexMaster.ThePrey.IntegrationEvents;

/// <summary>
/// The canonical server → client real-time protocol contract. Every message the server broadcasts to
/// a game's Web PubSub group is a versioned envelope:
/// <code>{ "v": &lt;version&gt;, "type": "&lt;message-type&gt;", "gameId": "&lt;guid&gt;", "seq": &lt;n&gt;, "data": { ... } }</code>
/// Both the Games module (which decides <em>what</em> changed) and the Notifications module (which
/// assembles the envelope and allocates <c>seq</c>) reference these constants so the wire contract
/// cannot drift. The two clients (Ionic, MAUI) mirror these exact strings — see
/// <c>docs/api/realtime.md</c>, which is the human-readable rendering of this contract.
/// </summary>
public static class RealtimeProtocol
{
    /// <summary>
    /// Protocol major version. Bumped only on a breaking envelope/catalog change. A client that does
    /// not support the received version ignores the message's incremental effect and forces a full
    /// snapshot resync instead.
    /// </summary>
    public const int Version = 1;

    /// <summary>The canonical message-type strings, grouped lobby / gameplay / control.</summary>
    public static class MessageTypes
    {
        // Lobby deltas — never full-game snapshots.
        /// <summary><c>data</c> = full <c>ParticipantDto</c>. A participant entered the lobby.</summary>
        public const string ParticipantJoined = "participant-joined";

        /// <summary><c>data</c> = full <c>ParticipantDto</c>. A participant's ready flag, callsign, role, state, or penalty changed.</summary>
        public const string ParticipantChanged = "participant-changed";

        /// <summary><c>data</c> = <c>{ userId }</c>. A participant left or was removed.</summary>
        public const string ParticipantRemoved = "participant-removed";

        /// <summary><c>data</c> = game-level slice (status + configuration + timing; no participants). Covers every status transition.</summary>
        public const string ConfigurationChanged = "configuration-changed";

        // Gameplay.
        /// <summary><c>data</c> = <c>{ locations: [ { userId, role, latitude, longitude, state } ] }</c> — one or more.</summary>
        public const string LocationsUpdated = "locations-updated";

        /// <summary><c>data</c> = <c>{ userId, event, state, penaltyEndsAt?, reason? }</c>. A prey was tagged or (un)penalized.</summary>
        public const string PreyUpdated = "prey-updated";

        /// <summary><c>data</c> = <c>{ outcome, survivorCount, completedAt? }</c>. Emitted exactly once per game.</summary>
        public const string GameEnded = "game-ended";

        // Control.
        /// <summary><c>data</c> = <c>{ reason }</c>. Server hint telling clients to pull a fresh full snapshot.</summary>
        public const string ResyncRequested = "resync-requested";
    }

    /// <summary>The values of the <c>event</c> discriminator carried by <see cref="MessageTypes.PreyUpdated"/>.</summary>
    public static class PreyEvents
    {
        public const string Tagged = "tagged";
        public const string Penalized = "penalized";
        public const string PenaltyCleared = "penalty-cleared";
    }
}
