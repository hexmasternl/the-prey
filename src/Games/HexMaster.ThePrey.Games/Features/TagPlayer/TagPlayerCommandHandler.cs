using System.Diagnostics;
using HexMaster.ThePrey.Core;
using HexMaster.ThePrey.Games;
using HexMaster.ThePrey.Games.Notifications;
using HexMaster.ThePrey.Games.Observability;

namespace HexMaster.ThePrey.Games.Features.TagPlayer;

public sealed class TagPlayerCommandHandler : ICommandHandler<TagPlayerCommand, TagPlayerResult?>
{
    private readonly IGameRepository _games;
    private readonly IGameEventBus _eventBus;
    private readonly IGameMetrics _metrics;
    private readonly TimeProvider _timeProvider;

    public TagPlayerCommandHandler(IGameRepository games, IGameEventBus eventBus, IGameMetrics metrics, TimeProvider timeProvider)
    {
        _games = games;
        _eventBus = eventBus;
        _metrics = metrics;
        _timeProvider = timeProvider;
    }

    public async Task<TagPlayerResult?> Handle(TagPlayerCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        var game = await _games.GetByIdAsync(command.GameId, ct);
        if (game is null)
            return null;

        using var activity = GameActivitySource.Source.StartActivity("TagPlayer");
        activity?.SetTag("game.id", game.Id);
        activity?.SetTag("game.tag.target_id", command.TargetParticipantId);

        try
        {
            var now = _timeProvider.GetUtcNow();
            game.TagParticipant(command.CallerId, command.TargetParticipantId, now);

            // Tagging the last surviving prey leaves no one in play — the hunters have won, so end
            // the game now rather than waiting for the scheduled-end sweep.
            var gameEnded = game.SurvivingPreyCount == 0;
            if (gameEnded)
                game.Complete(now);

            activity?.SetTag("game.ended", gameEnded);

            await _games.UpdateAsync(game, ct);

            await _eventBus.PublishAsync(game.Id,
                new ParticipantStatusChangedEvent(game.Id, command.TargetParticipantId, "Prey", "Tagged"), ct);

            if (gameEnded)
            {
                _metrics.RecordGameCompleted(game.Outcome.ToString());
                await _eventBus.PublishAsync(game.Id, game.ToGameEndedEvent(), ct);
            }

            return new TagPlayerResult();
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            throw;
        }
    }
}
