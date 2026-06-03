namespace HexMaster.ThePrey.Games.Abstractions.DataTransferObjects;

public sealed record LocationReadingDto(Guid Id, GpsCoordinateDto Coordinate, DateTimeOffset RecordedAt);
