using Bogus;
using HexMaster.ThePrey.PlayFields.DomainModels;

namespace HexMaster.ThePrey.PlayFields.Tests.Factories;

internal static class PlayFieldFaker
{
    private static readonly Faker _faker = new();

    /// <summary>A simple axis-aligned square polygon (4 valid points) around the given origin.</summary>
    internal static IReadOnlyList<GpsCoordinate> SquarePoints(double originLat = 52.0, double originLon = 5.0, double size = 0.01)
    {
        return
        [
            GpsCoordinate.Create(originLat, originLon),
            GpsCoordinate.Create(originLat, originLon + size),
            GpsCoordinate.Create(originLat + size, originLon + size),
            GpsCoordinate.Create(originLat + size, originLon)
        ];
    }

    internal static PlayField CreateValid(
        string? name = null,
        Guid? ownerId = null,
        bool isPublic = false,
        IReadOnlyList<GpsCoordinate>? points = null)
    {
        return PlayField.Create(
            name ?? _faker.Address.City(),
            ownerId ?? _faker.Random.Guid(),
            points ?? SquarePoints(),
            isPublic);
    }
}
