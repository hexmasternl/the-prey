using System.Diagnostics;
using HexMaster.ThePrey.Core;
using HexMaster.ThePrey.Games.Abstractions.DataTransferObjects;
using HexMaster.ThePrey.Games.DomainModels;
using HexMaster.ThePrey.Games.Observability;

namespace HexMaster.ThePrey.Games.Features.GetActiveGame;

public sealed class GetActiveGameQueryHandler : IQueryHandler<GetActiveGameQuery, GameStatusDto?>
{
    private readonly IGameRepository _games;
    private readonly IPlayfieldInfoProvider _playfields;

    public GetActiveGameQueryHandler(IGameRepository games, IPlayfieldInfoProvider playfields)
    {
        _games = games;
        _playfields = playfields;
    }

    public async Task<GameStatusDto?> Handle(GetActiveGameQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        using var activity = GameActivitySource.Source.StartActivity("GetActiveGame");

        try
        {
            var active = await _games.GetActiveGameForUserAsync(query.UserId, ct);

            activity?.SetTag("game.active", active is not null);

            if (active is null)
                return null;

            var playfieldInfo = await _playfields.GetAsync(active.PlayfieldId, ct);
            return active.ToStatusDto(playfieldInfo, query.UserId, DateTimeOffset.UtcNow);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            throw;
        }
    }
}
