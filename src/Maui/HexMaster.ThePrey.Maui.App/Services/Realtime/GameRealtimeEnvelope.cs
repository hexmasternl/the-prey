using System.Text.Json;

namespace HexMaster.ThePrey.Maui.App.Services.Realtime;

/// <summary>
/// A single real-time event as delivered over the game's Web PubSub group: the versioned envelope's
/// <see cref="Version"/> (<c>v</c>), <see cref="Type"/>, <see cref="GameId"/>, <see cref="Seq"/>, and its
/// still-raw <see cref="Data"/> payload. The connection wrapper parses the transport frame down to this
/// shape; the state service checks <see cref="Version"/> and <see cref="Seq"/> continuity before
/// deserializing <see cref="Data"/> into the matching typed payload based on <see cref="Type"/>.
/// <see cref="Data"/> is always a detached (<c>Clone()</c>d) element, safe to read after the source
/// document is disposed.
/// </summary>
public sealed record GameRealtimeEnvelope(
    string Type,
    JsonElement Data,
    int? Version = null,
    long? Seq = null,
    Guid? GameId = null);
