namespace HexMaster.ThePrey.Games.Abstractions.DataTransferObjects;

public sealed record LocationUpdateRequest(IReadOnlyList<ParticipantLocationDto> Locations);

public sealed record ParticipantLocationDto(Guid UserId, double Latitude, double Longitude);
