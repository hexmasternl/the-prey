using System.Diagnostics;
using HexMaster.ThePrey.Core;
using HexMaster.ThePrey.Games.Abstractions.DataTransferObjects;
using HexMaster.ThePrey.Games.DomainModels;
using HexMaster.ThePrey.Games.Notifications;
using HexMaster.ThePrey.Games.Observability;

namespace HexMaster.ThePrey.Games.Features.RecordPlayerLocation;

public sealed class RecordPlayerLocationCommandHandler : ICommandHandler<RecordPlayerLocationCommand, RecordPlayerLocationResult?>
{
    private readonly IGameRepository _games;
    private readonly IGameMetrics _metrics;
    private readonly IGameEventBus _eventBus;
    private readonly TimeProvider _timeProvider;

    public RecordPlayerLocationCommandHandler(
        IGameRepository games,
        IGameMetrics metrics,
        IGameEventBus eventBus,
        TimeProvider timeProvider)
    {
        _games = games;
        _metrics = metrics;
        _eventBus = eventBus;
        _timeProvider = timeProvider;
    }

    public async Task<RecordPlayerLocationResult?> Handle(RecordPlayerLocationCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        var game = await _games.GetByIdAsync(command.GameId, ct);
        if (game is null)
            return null;

        using var activity = GameActivitySource.Source.StartActivity("RecordPlayerLocation");
        activity?.SetTag("game.id", game.Id);
        activity?.SetTag("game.location_accuracy_meters", command.Accuracy);

        var now = _timeProvider.GetUtcNow();
        var recordedAt = command.RecordedAt ?? now;
        var coordinate = GpsCoordinate.Create(command.Latitude, command.Longitude);

        game.RecordLocation(command.UserId, coordinate, recordedAt);

        await _games.UpdateAsync(game, ct);

        _metrics.RecordLocationRecorded();

        if (game.Hunter?.UserId == command.UserId)
        {
            await _eventBus.PublishAsync(game.Id,
                new ParticipantLocatedEvent(game.Id, command.UserId, "Hunter", command.Latitude, command.Longitude), ct);
        }
        else if (game.Preys.Any(p => p.UserId == command.UserId))
        {
            await _eventBus.PublishAsync(game.Id,
                new ParticipantLocatedEvent(game.Id, command.UserId, "Prey", command.Latitude, command.Longitude), ct);
        }

        var nextInterval = game.RegularReportingIntervalAt(now);
        var penaltyEndsAt = game.ActivePenaltyEndsAtFor(command.UserId, now);
        var penaltyInterval = penaltyEndsAt is null ? (int?)null : Game.PenaltyReportingIntervalSeconds;

        return new RecordPlayerLocationResult(
            new RecordLocationResponse(true, nextInterval, penaltyInterval, penaltyEndsAt));
    }
}
