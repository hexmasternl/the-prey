namespace ThePrey.Application.App.Models;

public sealed class Playfield
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsPublic { get; set; }
    public string? OwnerId { get; set; }
    public List<PlayfieldCoordinate> Coordinates { get; set; } = [];
}

public sealed class PlayfieldCoordinate
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}
