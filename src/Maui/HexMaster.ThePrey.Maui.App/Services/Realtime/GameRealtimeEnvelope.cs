using System.Text.Json;

namespace HexMaster.ThePrey.Maui.App.Services.Realtime;

/// <summary>
/// A single real-time event as delivered over the game's Web PubSub group: the event
/// <see cref="Type"/> and its still-raw <see cref="Data"/> payload. The connection wrapper parses the
/// transport frame down to this shape; the state service deserializes <see cref="Data"/> into the
/// matching typed payload based on <see cref="Type"/>. <see cref="Data"/> is always a detached
/// (<c>Clone()</c>d) element, safe to read after the source document is disposed.
/// </summary>
public sealed record GameRealtimeEnvelope(string Type, JsonElement Data);
