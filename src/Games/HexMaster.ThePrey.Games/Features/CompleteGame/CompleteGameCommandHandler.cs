using System.Diagnostics;
using HexMaster.ThePrey.Core;
using HexMaster.ThePrey.Games.DomainModels;
using HexMaster.ThePrey.Games.Notifications;
using HexMaster.ThePrey.Games.Observability;
using HexMaster.ThePrey.Games;

namespace HexMaster.ThePrey.Games.Features.CompleteGame;

public sealed class CompleteGameCommandHandler : ICommandHandler<CompleteGameCommand, CompleteGameResult>
{
    private readonly IGameRepository _games;
    private readonly IGameEventBus _eventBus;
    private readonly IGameMetrics _metrics;
    private readonly TimeProvider _timeProvider;

    public CompleteGameCommandHandler(
        IGameRepository games,
        IGameEventBus eventBus,
        IGameMetrics metrics,
        TimeProvider timeProvider)
    {
        _games = games;
        _eventBus = eventBus;
        _metrics = metrics;
        _timeProvider = timeProvider;
    }

    public async Task<CompleteGameResult> Handle(CompleteGameCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var activity = GameActivitySource.Source.StartActivity("CompleteGame");
        activity?.SetTag("game.id", command.GameId);

        try
        {
            var game = await _games.GetByIdAsync(command.GameId, ct)
                ?? throw new KeyNotFoundException($"Game {command.GameId} not found.");

            // Idempotent: a game already completed by another path (e.g. owner force-end) is fine.
            if (game.Status == GameStatus.Completed)
            {
                activity?.SetTag("game.already_completed", true);
                return new CompleteGameResult(AlreadyCompleted: true);
            }

            var now = _timeProvider.GetUtcNow();
            game.Complete(now);

            await _games.UpdateAsync(game, ct);
            await _eventBus.PublishAsync(game.Id, game.ToGameEndedEvent(), ct);

            _metrics.RecordGameCompleted(game.Outcome.ToString());

            activity?.SetTag("game.outcome", game.Outcome.ToString());
            return new CompleteGameResult(AlreadyCompleted: false);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            throw;
        }
    }
}
