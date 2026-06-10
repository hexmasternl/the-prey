namespace HexMaster.ThePrey.Games.DomainModels;

/// <summary>
/// A participant whose broadcast ("last known") position was refreshed during a sweep tick.
/// Returned by <see cref="Game.RefreshBroadcastLocations"/> so the caller can notify clients.
/// </summary>
public sealed record BroadcastUpdate(Guid UserId, double Latitude, double Longitude, string State);
