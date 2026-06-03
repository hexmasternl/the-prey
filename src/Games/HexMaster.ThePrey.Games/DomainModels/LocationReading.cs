namespace HexMaster.ThePrey.Games.DomainModels;

/// <summary>A single GPS location a participant reported to the server, with the time it was recorded.</summary>
public sealed record LocationReading(Guid Id, GpsCoordinate Coordinate, DateTimeOffset RecordedAt)
{
    public static LocationReading Create(GpsCoordinate coordinate, DateTimeOffset recordedAt)
    {
        ArgumentNullException.ThrowIfNull(coordinate);
        return new LocationReading(Guid.NewGuid(), coordinate, recordedAt);
    }
}
