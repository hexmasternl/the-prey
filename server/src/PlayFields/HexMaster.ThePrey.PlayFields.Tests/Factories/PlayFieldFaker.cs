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

    /// <summary>
    /// A convention-compliant name suitable for public play fields (<c>CC, City, Fieldname</c> format).
    /// Used as the default name when <paramref name="isPublic"/> is <see langword="true"/> and no explicit
    /// name is supplied, so that <see cref="PlayField.IsPublicEligibleName"/> passes.
    /// </summary>
    internal const string EligiblePublicName = "NL, Amsterdam, Test Field";

    internal static PlayField CreateValid(
        string? name = null,
        Guid? ownerId = null,
        bool isPublic = false,
        IReadOnlyList<GpsCoordinate>? points = null)
    {
        var resolvedName = name ?? (isPublic ? EligiblePublicName : _faker.Address.City());
        return PlayField.Create(
            resolvedName,
            ownerId ?? _faker.Random.Guid(),
            points ?? SquarePoints(),
            isPublic);
    }

    /// <summary>Kilometres per degree of latitude, to place play fields a given distance north of a point.</summary>
    internal const double KilometresPerDegreeLatitude = 111.195;

    /// <summary>A public play field whose centre lies <paramref name="kilometresNorth"/> north of the given point.</summary>
    internal static PlayField CreatePublicNorthOf(
        double originLat,
        double originLon,
        double kilometresNorth,
        string? name = null)
    {
        const double size = 0.002;
        var centerLat = originLat + kilometresNorth / KilometresPerDegreeLatitude;
        return CreateValid(
            name: name ?? EligiblePublicName,
            isPublic: true,
            points: SquarePoints(centerLat - size / 2, originLon - size / 2, size));
    }

    /// <summary>A stored public play field from before centres were computed: no points, no centre.</summary>
    internal static PlayField CreatePublicWithoutCenter() =>
        PlayField.Rehydrate(_faker.Random.Guid(), EligiblePublicName, _faker.Random.Guid(), true, [], DateTimeOffset.UtcNow);
}
