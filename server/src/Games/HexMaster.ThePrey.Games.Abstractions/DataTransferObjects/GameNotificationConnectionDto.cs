namespace HexMaster.ThePrey.Games.Abstractions.DataTransferObjects;

/// <summary>
/// Returned by the game notifications token endpoint: the authenticated, group-scoped Web PubSub URL
/// (host + access token) the client uses to open a native WebSocket and join the game's group.
/// </summary>
public sealed record GameNotificationConnectionDto(string Url);
