using HexMaster.ThePrey.Core;
using HexMaster.ThePrey.Games.Abstractions.DataTransferObjects;

namespace HexMaster.ThePrey.Games.Features.GetGame;

public sealed class GetGameQueryHandler : IQueryHandler<GetGameQuery, GameDto?>
{
    private readonly IGameRepository _games;

    public GetGameQueryHandler(IGameRepository games) => _games = games;

    public async Task<GameDto?> Handle(GetGameQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var game = await _games.GetByIdAsync(query.GameId, ct);
        return game?.ToDto();
    }
}
