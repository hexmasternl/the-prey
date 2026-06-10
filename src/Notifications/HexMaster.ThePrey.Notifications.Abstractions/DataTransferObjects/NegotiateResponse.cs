namespace HexMaster.ThePrey.Notifications.Abstractions.DataTransferObjects;

/// <summary>
/// Returned by the negotiate endpoint: the authenticated, group-scoped URL the client uses to open a
/// Web PubSub WebSocket. The token embedded in the URL authorises joining the game's group.
/// </summary>
public sealed record NegotiateResponse(string Url);
