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

        // 2. Refresh last-known positions from new (unchecked) readings.
        var broadcasts = game.RefreshBroadcastLocations();
        foreach (var broadcast in broadcasts)
        {
            changed = true;
            events.Add(new PlayerLocationUpdatedIntegrationEvent(
                game.Id, broadcast.UserId, broadcast.Latitude, broadcast.Longitude, broadcast.State));
        }

        // 3. Boundary penalties for players currently outside the playfield.
        var penalties = await ApplyBoundaryPenaltiesAsync(game, now, events, ct);
        if (penalties > 0) changed = true;

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

        _metrics.RecordBroadcasts(broadcasts.Count);
        _metrics.RecordPenaltiesApplied(penalties);

        activity?.SetTag("game.tick.transitions", transitions.Count);
        activity?.SetTag("game.tick.broadcasts", broadcasts.Count);
        activity?.SetTag("game.tick.penalties", penalties);
        activity?.SetTag("game.tick.completed", completed);

        return new GameTickResult(transitions.Count, broadcasts.Count, penalties, completed);
    }

    private async Task<int> ApplyBoundaryPenaltiesAsync(
        Game game, DateTimeOffset now, List<IIntegrationEvent> events, CancellationToken ct)
    {
        var polygon = await _boundary.GetPolygonAsync(game.PlayfieldId, ct);
        if (polygon.Count < 3)
            return 0;

        var penalties = 0;
        var endsAt = now.AddSeconds(Game.PenaltyReportingIntervalSeconds);

        foreach (var participant in game.Participants)
        {
            if (participant.Location is null)
                continue;

            var isHunter = game.HunterUserId == participant.UserId;
            var enabled = isHunter
                ? game.Configuration.EnableHunterBoundaryPenalty
                : game.Configuration.EnablePreyBoundaryPenalties;

            if (!enabled || _checker.IsInside(polygon, participant.Location))
                continue;

            if (game.TryApplyBoundaryPenalty(participant.UserId, endsAt, now))
            {
                penalties++;
                events.Add(new PlayerPenalizedIntegrationEvent(game.Id, participant.UserId, endsAt, BoundaryPenaltyReason));
                _logger.LogInformation(
                    "Applied boundary penalty to {UserId} in game {GameId} until {EndsAt}",
                    participant.UserId, game.Id, endsAt);
            }
        }

        return penalties;
    }

    private static string RoleOf(Game game, Guid userId) => game.HunterUserId == userId ? "Hunter" : "Prey";
}
