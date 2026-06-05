namespace HexMaster.ThePrey.Games.Features.UpdateLocationBroadcast;

public sealed record UpdateLocationBroadcastCommand(
    Guid GameId,
    IReadOnlyList<ParticipantLocationUpdate> Locations);

public sealed record ParticipantLocationUpdate(Guid UserId, double Latitude, double Longitude);

public sealed record UpdateLocationBroadcastResult(bool Success);
