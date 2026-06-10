using System.Diagnostics;
using HexMaster.ThePrey.Core;
using HexMaster.ThePrey.Games.DomainModels;
using HexMaster.ThePrey.Games.Notifications;
using HexMaster.ThePrey.Games.Observability;
using HexMaster.ThePrey.Games;

namespace HexMaster.ThePrey.Games.Features.LeaveGame;

public sealed class LeaveGameCommandHandler : ICommandHandler<LeaveGameCommand, LeaveGameResult?>
{
    private readonly IGameRepository _games;
    private readonly IGameEventBus _eventBus;
    private readonly ILobbyEventBus _lobbyEventBus;
    private readonly TimeProvider _timeProvider;

    public LeaveGameCommandHandler(
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

    public async Task<LeaveGameResult?> Handle(LeaveGameCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var activity = GameActivitySource.Source.StartActivity("LeaveGame");
        activity?.SetTag("game.id", command.GameId);

        try
        {
            var game = await _games.GetByIdAsync(command.GameId, ct);
            if (game is null)
                return null;

            switch (game.Status)
            {
                case GameStatus.Lobby:
                    await HandleLobbyLeave(game, command.UserId, ct);
                    break;

                case GameStatus.InProgress:
                    await HandleInProgressLeave(game, command.UserId, ct);
                    break;

                case GameStatus.Completed:
                    throw new InvalidOperationException("Cannot leave a game that has already been completed.");

                default:
                    throw new InvalidOperationException($"Unhandled game status: {game.Status}.");
            }

            return new LeaveGameResult();
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            throw;
        }
    }

    private async Task HandleLobbyLeave(Game game, Guid userId, CancellationToken ct)
    {
        if (game.OwnerUserId == userId)
        {
            // Owner leaving the lobby cancels (ends) the game.
            game.EndByOwner(_timeProvider.GetUtcNow());
            await _games.UpdateAsync(game, ct);
            await _eventBus.PublishAsync(game.Id, game.ToGameEndedEvent(), ct);
            await _lobbyEventBus.PublishAsync(game.Id, "game-ended", game.ToDto(), ct);
        }
        else
        {
            if (!game.IsParticipant(userId))
                throw new ArgumentException("This user is not in the lobby.", nameof(userId));

            game.RemoveLobbyPlayer(userId);
            await _games.UpdateAsync(game, ct);
            await _lobbyEventBus.PublishAsync(game.Id, "lobby-updated", game.ToDto(), ct);
        }
    }

    private async Task HandleInProgressLeave(Game game, Guid userId, CancellationToken ct)
    {
        if (!game.IsParticipant(userId))
            throw new UnauthorizedAccessException("This user is not a participant of the game.");

        if (game.HunterUserId == userId)
        {
            // Hunter leaving an in-progress game ends it.
            game.EndByOwner(_timeProvider.GetUtcNow());
            await _games.UpdateAsync(game, ct);
            await _eventBus.PublishAsync(game.Id, game.ToGameEndedEvent(), ct);
        }
        else
        {
            // Prey forfeits.
            game.Forfeit(userId);
            await _games.UpdateAsync(game, ct);
            await _eventBus.PublishAsync(game.Id,
                new ParticipantStatusChangedEvent(game.Id, userId, "Prey", "Out"), ct);
        }
    }
}
