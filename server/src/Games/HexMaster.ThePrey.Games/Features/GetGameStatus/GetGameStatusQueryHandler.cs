using System.Diagnostics;
using HexMaster.ThePrey.Core;
using HexMaster.ThePrey.Games.Abstractions.DataTransferObjects;
using HexMaster.ThePrey.Games.DomainModels;
using HexMaster.ThePrey.Games.Observability;

namespace HexMaster.ThePrey.Games.Features.GetGameStatus;

public sealed class GetGameStatusQueryHandler : IQueryHandler<GetGameStatusQuery, GameStatusDto?>
{
    private readonly IGameRepository _games;
    private readonly IPlayfieldInfoProvider _playfields;

    public GetGameStatusQueryHandler(IGameRepository games, IPlayfieldInfoProvider playfields)
    {
        _games = games;
        _playfields = playfields;
    }

    public async Task<GameStatusDto?> Handle(GetGameStatusQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        using var activity = GameActivitySource.Source.StartActivity("GetGameStatus");
        activity?.SetTag("game.id", query.GameId);

        try
        {
            var game = await _games.GetByIdAsync(query.GameId, ct);
            if (game is null)
                return null;

            if (!game.IsParticipant(query.UserId))
                throw new UnauthorizedAccessException("Only participants of the game can retrieve its status.");

            if (game.Status != GameStatus.InProgress)
                throw new InvalidOperationException("Game status is only available for in-progress games.");

            var playfieldInfo = await _playfields.GetAsync(game.PlayfieldId, ct);

            return game.ToStatusDto(playfieldInfo, query.UserId, DateTimeOffset.UtcNow);
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException and not InvalidOperationException)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            throw;
        }
    }
}
