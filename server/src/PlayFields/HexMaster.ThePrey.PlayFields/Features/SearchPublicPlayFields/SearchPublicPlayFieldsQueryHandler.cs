using System.Diagnostics;
using HexMaster.ThePrey.Core;
using HexMaster.ThePrey.PlayFields.Abstractions.DataTransferObjects;
using HexMaster.ThePrey.PlayFields.DomainModels;
using HexMaster.ThePrey.PlayFields.Observability;
using Microsoft.Extensions.Logging;

namespace HexMaster.ThePrey.PlayFields.Features.SearchPublicPlayFields;

public sealed class SearchPublicPlayFieldsQueryHandler
    : IQueryHandler<SearchPublicPlayFieldsQuery, IReadOnlyList<PlayFieldSummaryDto>>
{
    private readonly IPlayFieldRepository _playFields;
    private readonly IPlayFieldMetrics _metrics;
    private readonly ILogger<SearchPublicPlayFieldsQueryHandler> _logger;

    public SearchPublicPlayFieldsQueryHandler(
        IPlayFieldRepository playFields,
        IPlayFieldMetrics metrics,
        ILogger<SearchPublicPlayFieldsQueryHandler> logger)
    {
        _playFields = playFields;
        _metrics = metrics;
        _logger = logger;
    }

    public async Task<IReadOnlyList<PlayFieldSummaryDto>> Handle(SearchPublicPlayFieldsQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        using var activity = PlayFieldActivitySource.Source.StartActivity("SearchPublicPlayFields");

        try
        {
            var origin = query.Origin;
            if (origin is not null)
            {
                // Validates the range; throws ArgumentOutOfRangeException for an invalid position.
                GpsCoordinate.Create(origin.Latitude, origin.Longitude);
            }

            // With a position a blank name means "no name filter"; without one the name is required.
            var searchText = string.IsNullOrWhiteSpace(query.SearchText) ? null : query.SearchText.Trim();
            if ((searchText is null && origin is null)
                || (searchText is not null && searchText.Length < SearchPublicPlayFieldsQuery.MinimumSearchLength))
            {
                throw new ArgumentException(
                    $"The search text must be at least {SearchPublicPlayFieldsQuery.MinimumSearchLength} characters long.",
                    nameof(query.SearchText));
            }

            var mode = origin is null ? SearchModes.Name : searchText is null ? SearchModes.Near : SearchModes.Both;
            var radiusKm = ClampRadiusKm(query.RadiusKm);
            // Low-cardinality only: never the raw query text, coordinates or user ids.
            activity?.SetTag("playfield.search.mode", mode);
            if (origin is not null) activity?.SetTag("playfield.search.radius_km", (int)radiusKm);

            var playFields = await _playFields.SearchPublicAsync(searchText, ct);

            var results = origin is null
                ? playFields.Select(p => p.ToSummaryDto()).ToList()
                : Nearby(playFields, origin, radiusKm);

            _metrics.RecordPublicPlayFieldSearch(mode);
            _logger.LogInformation("Public play-field search ({SearchMode}) returned {ResultCount} results", mode, results.Count);

            activity?.SetTag("playfield.search.result_count", results.Count);

            return results;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            throw;
        }
    }

    /// <summary>The telemetry values for <c>playfield.search.mode</c>.</summary>
    internal static class SearchModes
    {
        internal const string Name = "name";
        internal const string Near = "near";
        internal const string Both = "both";
    }

    /// <summary>The radius actually used: out-of-range values are clamped, not rejected.</summary>
    internal static double ClampRadiusKm(double radiusKm) =>
        double.IsNaN(radiusKm)
            ? SearchPublicPlayFieldsQuery.DefaultRadiusKm
            : Math.Clamp(radiusKm, SearchPublicPlayFieldsQuery.MinimumRadiusKm, SearchPublicPlayFieldsQuery.MaximumRadiusKm);

    /// <summary>Play fields whose centre lies within the radius, nearest first, capped; those without a centre are left out.</summary>
    private static List<PlayFieldSummaryDto> Nearby(IEnumerable<PlayField> playFields, GpsCoordinate origin, double radiusKm)
    {
        var radiusMeters = radiusKm * 1000;
        return playFields
            .Where(p => p.CenterCoordinates is not null)
            .Select(p => (PlayField: p, Meters: GeoDistance.Meters(origin, p.CenterCoordinates!)))
            .Where(x => x.Meters <= radiusMeters)
            .OrderBy(x => x.Meters)
            .Take(SearchPublicPlayFieldsQuery.MaximumNearbyResults)
            .Select(x => x.PlayField.ToSummaryDto((int)Math.Round(x.Meters)))
            .ToList();
    }
}
