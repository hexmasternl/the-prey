namespace HexMaster.ThePrey.Games.DomainModels;

/// <summary>
/// A participant's broadcast ("last known") position as of a sweep tick.
/// Returned by <see cref="Game.SweepLocations"/> so the caller can notify clients.
/// </summary>
public sealed record BroadcastUpdate(Guid UserId, double Latitude, double Longitude, string State);
