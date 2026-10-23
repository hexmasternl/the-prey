using System.Diagnostics;
using HexMaster.ThePrey.Core;
using HexMaster.ThePrey.PlayFields.Abstractions.DataTransferObjects;
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
            if (string.IsNullOrWhiteSpace(query.SearchText)
                || query.SearchText.Trim().Length < SearchPublicPlayFieldsQuery.MinimumSearchLength)
            {
                throw new ArgumentException(
                    $"The search text must be at least {SearchPublicPlayFieldsQuery.MinimumSearchLength} characters long.",
                    nameof(query.SearchText));
            }

            var playFields = await _playFields.SearchPublicAsync(query.SearchText.Trim(), ct);

            _metrics.RecordPublicPlayFieldSearch();
            _logger.LogInformation("Public play-field search returned {ResultCount} results", playFields.Count);

            // Low-cardinality only: the result count, never the raw query text or user ids.
            activity?.SetTag("playfield.search.result_count", playFields.Count);

            return playFields.Select(p => p.ToSummaryDto()).ToList();
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            throw;
        }
    }
}
