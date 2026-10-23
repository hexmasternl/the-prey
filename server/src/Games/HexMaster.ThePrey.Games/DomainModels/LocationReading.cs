namespace HexMaster.ThePrey.Games.DomainModels;

/// <summary>
/// A single GPS location a participant reported to the server, with the time it was recorded.
/// <see cref="Checked"/> records whether the periodic game sweep has already processed this reading
/// (boundary check + last-known-position refresh); it lets the sweep ignore readings it has seen and
/// survives a leader failover because it is persisted (unlike in-memory broadcast state).
/// </summary>
public sealed record LocationReading(Guid Id, GpsCoordinate Coordinate, DateTimeOffset RecordedAt, bool Checked = false)
{
    public static LocationReading Create(GpsCoordinate coordinate, DateTimeOffset recordedAt)
    {
        ArgumentNullException.ThrowIfNull(coordinate);
        return new LocationReading(Guid.NewGuid(), coordinate, recordedAt);
    }

    /// <summary>Returns a copy of this reading marked as processed by the sweep.</summary>
    public LocationReading MarkChecked() => this with { Checked = true };
}
