using HexMaster.ThePrey.Games.Abstractions.DataTransferObjects;

namespace HexMaster.ThePrey.Games.Features.RecordPlayerLocation;

public sealed record RecordPlayerLocationCommand(
    Guid GameId,
    Guid UserId,
    double Latitude,
    double Longitude,
    DateTimeOffset? RecordedAt);

public sealed record RecordPlayerLocationResult(RecordLocationResponse Response);
