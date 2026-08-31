using HexMaster.ThePrey.Core;
using HexMaster.ThePrey.Games.Abstractions.DataTransferObjects;
using HexMaster.ThePrey.Games.Observability;

namespace HexMaster.ThePrey.Games.Features.GetGameState;

public sealed class GetGameStateQueryHandler : IQueryHandler<GetGameStateQuery, GameStateDto?>
{
    private readonly IGameRepository _games;

    public GetGameStateQueryHandler(IGameRepository games) => _games = games;

    public async Task<GameStateDto?> Handle(GetGameStateQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var game = await _games.GetByIdAsync(query.GameId, ct);
        if (game is null)
            return null;

        using var activity = GameActivitySource.Source.StartActivity("GetGameState");
        activity?.SetTag("game.id", game.Id);
        activity?.SetTag("game.user_id", query.UserId);

        // Null when the game is not in progress or the caller is neither hunter nor prey.
        return game.ToRoleStateDto(query.UserId);
    }
}
