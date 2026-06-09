using System.Diagnostics;
using HexMaster.ThePrey.Core;
using HexMaster.ThePrey.Games.DomainModels;
using HexMaster.ThePrey.Games.Notifications;
using HexMaster.ThePrey.Games.Observability;

namespace HexMaster.ThePrey.Games.Features.EndGame;

public sealed class EndGameCommandHandler : ICommandHandler<EndGameCommand, EndGameResult?>
{
    private readonly IGameRepository _games;
    private readonly IGameEventBus _eventBus;
    private readonly ILobbyEventBus _lobbyEventBus;
    private readonly TimeProvider _timeProvider;

    public EndGameCommandHandler(
        IGameRepository games,
        IGameEventBus eventBus,
        ILobbyEventBus lobbyEventBus,
        TimeProvider timeProvider)
    {
        _games = games;
        _eventBus = eventBus;
        _lobbyEventBus = lobbyEventBus;
        _timeProvider = timeProvider;
    }

    public async Task<EndGameResult?> Handle(EndGameCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var activity = GameActivitySource.Source.StartActivity("EndGame");
        activity?.SetTag("game.id", command.GameId);

        try
        {
            var game = await _games.GetByIdAsync(command.GameId, ct);
            if (game is null)
                return null;

            if (game.OwnerUserId != command.RequestingUserId)
                throw new UnauthorizedAccessException("Only the game owner can force-end the game.");

            var wasInLobby = game.Status == GameStatus.Lobby;

            game.EndByOwner(_timeProvider.GetUtcNow());

            await _games.UpdateAsync(game, ct);

            await _eventBus.PublishAsync(game.Id, new GameEndedEvent(game.Id), ct);

            if (wasInLobby)
                await _lobbyEventBus.PublishAsync(game.Id, "game-ended", game.ToDto(), ct);

            return new EndGameResult();
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            throw;
        }
    }
}
