using HexMaster.ThePrey.Core;
using HexMaster.ThePrey.Games.Abstractions.DataTransferObjects;

namespace HexMaster.ThePrey.Games.Features.ListGames;

public sealed class ListGamesQueryHandler : IQueryHandler<ListGamesQuery, IReadOnlyList<GameSummaryDto>>
{
    private readonly IGameRepository _games;

    public ListGamesQueryHandler(IGameRepository games) => _games = games;

    public async Task<IReadOnlyList<GameSummaryDto>> Handle(ListGamesQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var games = await _games.ListForUserAsync(query.RequestingUserId, ct);

        return games.Select(g => g.ToSummaryDto()).ToList();
    }
}
