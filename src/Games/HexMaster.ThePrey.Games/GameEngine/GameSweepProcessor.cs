using System.Diagnostics;
using HexMaster.ThePrey.Games.Abstractions.DataTransferObjects;
using HexMaster.ThePrey.Games.DomainModels;
using HexMaster.ThePrey.Games.Observability;
using HexMaster.ThePrey.IntegrationEvents;
using HexMaster.ThePrey.IntegrationEvents.Events;
using Microsoft.Extensions.Logging;

namespace HexMaster.ThePrey.Games.GameEngine;

/// <summary>
/// Default <see cref="IGameSweepProcessor"/>. Scoped: it uses the scoped <see cref="IGameRepository"/>
/// so each game is loaded and persisted on its own <c>DbContext</c>, which keeps the parallel sweep
/// thread-safe.
/// </summary>
public sealed class GameSweepProcessor : IGameSweepProcessor
{
    private const string BoundaryPenaltyReason = "left-playfield";
    private const string HeadStartPenaltyReason = "moved-during-delay";

    private readonly IGameRepository _games;
    private readonly IPlayfieldBoundaryProvider _boundary;
    private readonly IBoundaryChecker _checker;
    private readonly IIntegrationEventPublisher _publisher;
    private readonly IGameMetrics _metrics;
    private readonly ILogger<GameSweepProcessor> _logger;

    public GameSweepProcessor(
        IGameRepository games,
        IPlayfieldBoundaryProvider boundary,
        IBoundaryChecker checker,
        IIntegrationEventPublisher publisher,
        IGameMetrics metrics,
        ILogger<GameSweepProcessor> logger)
    {
        _games = games;
        _boundary = boundary;
        _checker = checker;
        _publisher = publisher;
        _metrics = metrics;
        _logger = logger;
    }

    public async Task<GameTickResult> ProcessAsync(Guid gameId, DateTimeOffset now, CancellationToken ct)
    {
        using var activity = GameActivitySource.Source.StartActivity("GameTick.ProcessGame");
        activity?.SetTag("game.id", gameId);

        var game = await _games.GetByIdAsync(gameId, ct);
        if (game is null)
            return GameTickResult.None;

        var events = new List<IIntegrationEvent>();
        var changed = false;

        // 0. Promote Started games to InProgress as the FIRST task of every tick.
        //    StartedAt is backdated 3 s so every derived deadline is already in the past relative to
        //    this sweep's clock, guaranteeing the first broadcast and timeout passes run immediately.
        if (game.Status == GameStatus.Started)
        {
            game.BeginPlay(now - TimeSpan.FromSeconds(3));
            changed = true;
            events.Add(new GameNotificationIntegrationEvent(game.Id, RealtimeProtocol.MessageTypes.ConfigurationChanged,
                game.ToConfigurationChangedDto()));
            activity?.SetTag("game.tick.promoted", true);
        }

        if (game.Status != GameStatus.InProgress)
            return GameTickResult.None;

        // 1. Player status transitions (folds in the old PlayerStateMonitor responsibility).
        var transitions = game.ApplyTimeoutTransitions(now);
        foreach (var (userId, _) in transitions)
        {
            changed = true;
            events.Add(new GameNotificationIntegrationEvent(game.Id, RealtimeProtocol.MessageTypes.ParticipantChanged,
                game.ToParticipantDto(userId)));
        }

        // 2. Consume new (unchecked) readings. The sweep advances NextScheduledBroadcastOn and
        //    copies the latest coordinate into Location for every participant that is broadcast
        //    this tick (regular schedule or active penalty). All of a tick's broadcasts are batched
        //    into a single locations-updated message rather than one message per coordinate.
        var sweeps = game.SweepLocations(now);
        var broadcasts = 0;
        var locations = new List<ParticipantLocationDto>();
        foreach (var sweep in sweeps)
        {
            if (sweep.NewCoordinates.Count > 0)
                changed = true;

            if (sweep.Broadcast is { } broadcast)
            {
                broadcasts++;
                changed = true; // broadcast mutates Location and may advance NextScheduledBroadcastOn
                locations.Add(game.ToParticipantLocationDto(broadcast.UserId, broadcast.Latitude, broadcast.Longitude, broadcast.State));
            }
        }

        if (locations.Count > 0)
            events.Add(new GameNotificationIntegrationEvent(game.Id, RealtimeProtocol.MessageTypes.LocationsUpdated,
                new LocationsUpdatedDto(locations)));

        // 3. Boundary penalties: every reading consumed this sweep is assessed.
        var penalties = await ApplyBoundaryPenaltiesAsync(game, sweeps, now, events, ct);
        if (penalties > 0) changed = true;

        // 3b. Hunter head-start penalty: assessed every sweep (idempotent).
        var headStartPenalty = game.AssessHunterHeadStartPenalty(now);
        if (headStartPenalty is { } hsPenalty && game.HunterUserId is { } hsPenaltyHunterId)
        {
            penalties++;
            changed = true;
            events.Add(new GameNotificationIntegrationEvent(game.Id, RealtimeProtocol.MessageTypes.PreyUpdated,
                new PreyUpdatedDto(hsPenaltyHunterId, RealtimeProtocol.PreyEvents.Penalized, null, hsPenalty.EndsAt, HeadStartPenaltyReason)));
        }

        // 4. Completion when the scheduled end time has passed.
        var completed = false;
        if (game.ScheduledEndAt is { } end && now >= end)
        {
            game.Complete(now);
            changed = true;
            completed = true;
            _metrics.RecordGameCompleted(game.Outcome.ToString());
            events.Add(new GameNotificationIntegrationEvent(game.Id, RealtimeProtocol.MessageTypes.GameEnded,
                game.ToGameEndedNotificationDto()));
        }

        // 5. Persist once, then notify (so clients never see state we failed to save).
        if (changed)
            await _games.UpdateAsync(game, ct);

        foreach (var integrationEvent in events)
            await _publisher.PublishAsync(integrationEvent, ct);

        _metrics.RecordBroadcasts(broadcasts);
        _metrics.RecordPenaltiesApplied(penalties);

        activity?.SetTag("game.tick.transitions", transitions.Count);
        activity?.SetTag("game.tick.broadcasts", broadcasts);
        activity?.SetTag("game.tick.penalties", penalties);
        activity?.SetTag("game.tick.completed", completed);

        return new GameTickResult(transitions.Count, broadcasts, penalties, completed);
    }

    private async Task<int> ApplyBoundaryPenaltiesAsync(
        Game game,
        IReadOnlyList<ParticipantLocationSweep> sweeps,
        DateTimeOffset now,
        List<IIntegrationEvent> events,
        CancellationToken ct)
    {
        var polygon = await _boundary.GetPolygonAsync(game.PlayfieldId, ct);
        if (polygon.Count < 3)
            return 0;

        var newCoordinates = sweeps.ToDictionary(s => s.UserId, s => s.NewCoordinates);
        var penalties = 0;

        foreach (var participant in game.Participants)
        {
            var isHunter = game.HunterUserId == participant.UserId;
            var enabled = isHunter
                ? game.Configuration.EnableHunterBoundaryPenalty
                : game.Configuration.EnablePreyBoundaryPenalties;
            if (!enabled)
                continue;

            // Assess every reading consumed this sweep; a participant without new readings is
            // assessed on their last-known position, so a player who stops reporting while outside
            // does not evade the penalty.
            IReadOnlyList<GpsCoordinate> coordinates =
                newCoordinates.TryGetValue(participant.UserId, out var consumed) && consumed.Count > 0
                    ? consumed
                    : participant.Location is { } lastKnown ? [lastKnown] : [];

            if (coordinates.All(c => _checker.IsInside(polygon, c)))
                continue;

            if (game.TryApplyBoundaryPenalty(participant.UserId, now) is { } penalty)
            {
                penalties++;
                events.Add(new GameNotificationIntegrationEvent(game.Id, RealtimeProtocol.MessageTypes.PreyUpdated,
                    new PreyUpdatedDto(participant.UserId, RealtimeProtocol.PreyEvents.Penalized, null, penalty.EndsAt, BoundaryPenaltyReason)));
                _logger.LogInformation(
                    "Applied boundary penalty to {UserId} in game {GameId} until {EndsAt}",
                    participant.UserId, game.Id, penalty.EndsAt);
            }
        }

        return penalties;
    }
}
