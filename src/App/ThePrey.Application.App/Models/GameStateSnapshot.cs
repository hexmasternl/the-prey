namespace ThePrey.Application.App.Models;

/// <summary>
/// The role-specific game state returned by <c>GET /games/{gameId}/state</c>.
/// Preys receive <see cref="HunterDistanceMeters"/>; hunters receive <see cref="PreyLocations"/>.
/// </summary>
public sealed class GameStateSnapshot
{
    public int? HunterDistanceMeters { get; set; }
    public List<GameCoordinate> PreyLocations { get; set; } = [];
}
