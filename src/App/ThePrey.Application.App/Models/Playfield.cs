namespace ThePrey.Application.App.Models;

public sealed class Playfield
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsPublic { get; set; }
    public string? OwnerId { get; set; }
    public string? OwnerName { get; set; }
    public List<PlayfieldCoordinate> Coordinates { get; set; } = [];
    public DateTimeOffset LastUpdatedOn { get; set; }
    public bool IsSynchronized { get; set; }
    public PlayfieldCoordinate? CenterCoordinates { get; set; }

    public PlayfieldCoordinate? ComputeCenter()
    {
        if (Coordinates.Count == 0) return null;
        var lat = Coordinates.Average(c => c.Latitude);
        var lon = Coordinates.Average(c => c.Longitude);
        return new PlayfieldCoordinate { Latitude = lat, Longitude = lon };
    }
}

public sealed class PlayfieldCoordinate
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}
