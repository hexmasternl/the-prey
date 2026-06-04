namespace ThePrey.Application.App.Models;

/// <summary>A GPS coordinate received from the game-state endpoint (e.g. a prey position).</summary>
public sealed class GameCoordinate
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}
