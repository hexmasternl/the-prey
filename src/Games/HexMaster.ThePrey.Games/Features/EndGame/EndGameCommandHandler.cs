using System.Diagnostics;
using HexMaster.ThePrey.Core;
using HexMaster.ThePrey.Games.Notifications;
using HexMaster.ThePrey.Games.Observability;
using HexMaster.ThePrey.IntegrationEvents;

namespace HexMaster.ThePrey.Games.Features.EndGame;

public sealed class EndGameCommandHandler : ICommandHandler<EndGameCommand, EndGameResult?>
{
    private readonly IGameRepository _games;
    private readonly IGameEventBus _eventBus;
    private readonly TimeProvider _timeProvider;

    public EndGameCommandHandler(
        IGameRepository games,
        IGameEventBus eventBus,
        TimeProvider timeProvider)
    {
        _games = games;
        _eventBus = eventBus;
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

            game.EndByOwner(_timeProvider.GetUtcNow());

            await _games.UpdateAsync(game, ct);

            await _eventBus.PublishAsync(game.Id, RealtimeProtocol.MessageTypes.GameEnded, game.ToGameEndedNotificationDto(), ct);

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
