using HexMaster.ThePrey.PlayFields.DomainModels;

namespace HexMaster.ThePrey.PlayFields.Features.SearchPublicPlayFields;

/// <summary>
/// A public play-field search by name (<paramref name="SearchText"/>), near a position
/// (<paramref name="Origin"/>), or both. <paramref name="RadiusKm"/> only applies with an origin and is
/// clamped to <see cref="MinimumRadiusKm"/>–<see cref="MaximumRadiusKm"/>.
/// </summary>
public sealed record SearchPublicPlayFieldsQuery(
    string? SearchText,
    GpsCoordinate? Origin = null,
    double RadiusKm = SearchPublicPlayFieldsQuery.DefaultRadiusKm)
{
    /// <summary>Minimum search length; matches the client-side minimum.</summary>
    public const int MinimumSearchLength = 3;

    public const double DefaultRadiusKm = 25;
    public const double MinimumRadiusKm = 1;
    public const double MaximumRadiusKm = 100;

    /// <summary>The most results a search with an origin returns, nearest first.</summary>
    public const int MaximumNearbyResults = 50;
}
