namespace HexMaster.ThePrey.Games.DomainModels;

/// <summary>
/// Per-participant outcome of <see cref="Game.SweepLocations"/>: the broadcast to send to clients
/// (null when there is nothing to broadcast) and every coordinate consumed from the unchecked
/// reading history, in chronological order, so the caller can assess boundary violations.
/// </summary>
public sealed record ParticipantLocationSweep(
    Guid UserId,
    BroadcastUpdate? Broadcast,
    IReadOnlyList<GpsCoordinate> NewCoordinates);
