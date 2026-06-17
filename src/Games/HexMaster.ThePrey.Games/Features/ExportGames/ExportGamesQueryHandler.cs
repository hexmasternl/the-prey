using System.Diagnostics;
using HexMaster.ThePrey.Core;
using HexMaster.ThePrey.Games.Abstractions.DataTransferObjects;
using HexMaster.ThePrey.Games.Observability;

namespace HexMaster.ThePrey.Games.Features.ExportGames;

public sealed class ExportGamesQueryHandler : IQueryHandler<ExportGamesQuery, IReadOnlyList<GameExportDto>>
{
    private readonly IGameRepository _games;

    public ExportGamesQueryHandler(IGameRepository games) => _games = games;

    public async Task<IReadOnlyList<GameExportDto>> Handle(ExportGamesQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        using var activity = GameActivitySource.Source.StartActivity("ExportGames");

        try
        {
            var games = await _games.GetGamesStartedBetweenAsync(query.FromInclusive, query.ToExclusive, ct);
            var result = games.Select(g => g.ToExportDto()).ToList();

            activity?.SetTag("games.export.count", result.Count);

            return result;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            throw;
        }
    }
}
