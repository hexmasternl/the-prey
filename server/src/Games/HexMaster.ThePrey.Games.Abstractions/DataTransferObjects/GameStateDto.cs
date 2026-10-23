namespace HexMaster.ThePrey.Games.Abstractions.DataTransferObjects;

/// <summary>
/// The role-specific, periodically polled state of an in-progress game.
/// Preys receive <see cref="HunterDistanceMeters"/> (null while the hunter has no known location);
/// hunters receive <see cref="PreyLocations"/>. Role-irrelevant fields are never populated.
/// </summary>
public sealed record GameStateDto(
    int? HunterDistanceMeters,
    IReadOnlyList<GpsCoordinateDto> PreyLocations);
