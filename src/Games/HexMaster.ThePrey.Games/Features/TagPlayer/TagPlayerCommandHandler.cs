using System.Diagnostics;
using HexMaster.ThePrey.Core;
using HexMaster.ThePrey.Games.Notifications;
using HexMaster.ThePrey.Games.Observability;

namespace HexMaster.ThePrey.Games.Features.TagPlayer;

public sealed class TagPlayerCommandHandler : ICommandHandler<TagPlayerCommand, TagPlayerResult?>
{
    private readonly IGameRepository _games;
    private readonly IGameEventBus _eventBus;
    private readonly TimeProvider _timeProvider;

    public TagPlayerCommandHandler(IGameRepository games, IGameEventBus eventBus, TimeProvider timeProvider)
    {
        _games = games;
        _eventBus = eventBus;
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
            game.TagParticipant(command.CallerId, command.TargetParticipantId, _timeProvider.GetUtcNow());
            await _games.UpdateAsync(game, ct);

            await _eventBus.PublishAsync(game.Id,
                new ParticipantStatusChangedEvent(game.Id, command.TargetParticipantId, "Prey", "Tagged"), ct);

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
