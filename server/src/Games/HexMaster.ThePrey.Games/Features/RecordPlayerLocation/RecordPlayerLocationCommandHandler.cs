using System.Diagnostics;
using HexMaster.ThePrey.Core;
using HexMaster.ThePrey.Games.Abstractions.DataTransferObjects;
using HexMaster.ThePrey.Games.DomainModels;
using HexMaster.ThePrey.Games.Notifications;
using HexMaster.ThePrey.Games.Observability;
using HexMaster.ThePrey.IntegrationEvents;

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

        var previousState = game.RecordLocation(command.UserId, coordinate, recordedAt);

        await _games.UpdateAsync(game, ct);

        _metrics.RecordLocationRecorded();

        // Location broadcasts are server-authoritative: only the 30s sweep broadcasts positions.
        // The handler only publishes the Passive→Active status change (not a location event).
        var isHunter = game.HunterUserId == command.UserId;
        if (!isHunter)
        {
            var prey = game.Participants.FirstOrDefault(p => p.UserId == command.UserId);
            if (prey is not null && previousState == PlayerState.Passive && prey.State == PlayerState.Active)
            {
                await _eventBus.PublishAsync(game.Id, RealtimeProtocol.MessageTypes.ParticipantChanged,
                    game.ToParticipantDto(command.UserId), ct);
            }
        }

        var penaltyEndsAt = game.ActivePenaltyEndsAtFor(command.UserId, now);
        var penaltyInterval = penaltyEndsAt is null ? (int?)null : Game.PenaltyReportingIntervalSeconds;

        return new RecordPlayerLocationResult(
            new RecordLocationResponse(true, Game.LocationReportingIntervalSeconds, penaltyInterval, penaltyEndsAt));
    }
}
