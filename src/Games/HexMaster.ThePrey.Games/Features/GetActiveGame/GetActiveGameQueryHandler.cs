using System.Diagnostics;
using HexMaster.ThePrey.Core;
using HexMaster.ThePrey.Games.Abstractions.DataTransferObjects;
using HexMaster.ThePrey.Games.DomainModels;
using HexMaster.ThePrey.Games.Observability;

namespace HexMaster.ThePrey.Games.Features.GetActiveGame;

public sealed class GetActiveGameQueryHandler : IQueryHandler<GetActiveGameQuery, ActiveGameDto?>
{
    private readonly IGameRepository _games;

    public GetActiveGameQueryHandler(IGameRepository games) => _games = games;

    public async Task<ActiveGameDto?> Handle(GetActiveGameQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        using var activity = GameActivitySource.Source.StartActivity("GetActiveGame");

        try
        {
            var games = await _games.ListForUserAsync(query.UserId, ct);
            var active = games.FirstOrDefault(g => g.Status == GameStatus.InProgress);

            activity?.SetTag("game.active", active is not null);

            return active is not null ? new ActiveGameDto(active.Id) : null;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            throw;
        }
    }
}
