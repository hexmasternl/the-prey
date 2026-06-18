using System.Diagnostics;
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
        if (game is null || game.Status != GameStatus.InProgress)
            return GameTickResult.None;

        var events = new List<IIntegrationEvent>();
        var changed = false;

        // 1. Player status transitions (folds in the old PlayerStateMonitor responsibility).
        var transitions = game.ApplyTimeoutTransitions(now);
        foreach (var (userId, newState) in transitions)
        {
            changed = true;
            events.Add(new PlayerStatusChangedIntegrationEvent(game.Id, userId, RoleOf(game, userId), newState.ToString()));
        }

        // 2. Consume new (unchecked) readings. The sweep advances NextScheduledBroadcastOn and
        //    copies the latest coordinate into Location for every participant that is broadcast
        //    this tick (regular schedule or active penalty).
        var sweeps = game.SweepLocations(now);
        var broadcasts = 0;
        foreach (var sweep in sweeps)
        {
            if (sweep.NewCoordinates.Count > 0)
                changed = true;

            if (sweep.Broadcast is { } broadcast)
            {
                broadcasts++;
                changed = true; // broadcast mutates Location and may advance NextScheduledBroadcastOn
                events.Add(new PlayerLocationUpdatedIntegrationEvent(
                    game.Id, broadcast.UserId, broadcast.Latitude, broadcast.Longitude, broadcast.State));
            }
        }

        // 3. Boundary penalties: every reading consumed this sweep is assessed.
        var penalties = await ApplyBoundaryPenaltiesAsync(game, sweeps, now, events, ct);
        if (penalties > 0) changed = true;

        // 3b. Hunter head-start penalty: assessed every sweep (idempotent).
        var headStartPenalty = game.AssessHunterHeadStartPenalty(now);
        if (headStartPenalty is { } hsPenalty && game.HunterUserId is { } hsPenaltyHunterId)
        {
            penalties++;
            changed = true;
            events.Add(new PlayerPenalizedIntegrationEvent(game.Id, hsPenaltyHunterId, hsPenalty.EndsAt, HeadStartPenaltyReason));
        }

        // 4. Completion when the scheduled end time has passed.
        var completed = false;
        if (game.ScheduledEndAt is { } end && now >= end)
        {
            game.Complete(now);
            changed = true;
            completed = true;
            _metrics.RecordGameCompleted(game.Outcome.ToString());
            events.Add(new GameEndedIntegrationEvent(game.Id, game.Outcome.ToString(), game.SurvivingPreyCount));
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
                events.Add(new PlayerPenalizedIntegrationEvent(game.Id, participant.UserId, penalty.EndsAt, BoundaryPenaltyReason));
                _logger.LogInformation(
                    "Applied boundary penalty to {UserId} in game {GameId} until {EndsAt}",
                    participant.UserId, game.Id, penalty.EndsAt);
            }
        }

        return penalties;
    }

    private static string RoleOf(Game game, Guid userId) => game.HunterUserId == userId ? "Hunter" : "Prey";
}
