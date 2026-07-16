namespace HexMaster.ThePrey.Maui.App.Services.Api;

/// <summary>
/// Client-side projection of the backend role-specific <c>GameStateDto</c> returned by
/// <c>GET /games/{id}/state</c>. A prey receives <see cref="HunterDistanceMeters"/> (null while the
/// hunter has no known location); a hunter receives <see cref="PreyLocations"/>. Deserializes
/// case-insensitively from the backend's camelCase JSON.
/// </summary>
public sealed record GameStateSnapshot(
    int? HunterDistanceMeters,
    IReadOnlyList<GpsCoordinate> PreyLocations);
