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

    /// <summary>
    /// Builds the query from the HTTP parameters <c>q</c>, <c>lat</c>, <c>lon</c> and <c>radiusKm</c>, or
    /// returns the validation errors keyed on the offending parameter. A blank <c>q</c> with a position
    /// means no name filter; a radius out of range is clamped later, not rejected.
    /// </summary>
    public static SearchPublicPlayFieldsQuery? FromRequest(
        string? q,
        double? lat,
        double? lon,
        double? radiusKm,
        out Dictionary<string, string[]> errors)
    {
        errors = [];
        if (lat is null && lon is not null) errors["lat"] = ["lat and lon must be given together."];
        if (lon is null && lat is not null) errors["lon"] = ["lat and lon must be given together."];
        if (lat is < -90 or > 90 || (lat is { } la && double.IsNaN(la))) errors["lat"] = ["lat must be between -90 and 90."];
        if (lon is < -180 or > 180 || (lon is { } lo && double.IsNaN(lo))) errors["lon"] = ["lon must be between -180 and 180."];

        var hasPosition = lat is not null && lon is not null;
        var text = string.IsNullOrWhiteSpace(q) ? null : q.Trim();
        if ((text is null && !hasPosition && errors.Count == 0) || text is { Length: < MinimumSearchLength })
            errors["q"] = [$"The search text must be at least {MinimumSearchLength} characters long."];

        if (errors.Count > 0) return null;

        return new SearchPublicPlayFieldsQuery(
            text,
            hasPosition ? GpsCoordinate.Create(lat!.Value, lon!.Value) : null,
            radiusKm ?? DefaultRadiusKm);
    }
}
