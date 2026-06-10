using System.Diagnostics;
using System.Net.Http.Json;
using HexMaster.ThePrey.GameEngine.Observability;
using HexMaster.ThePrey.Games.Data.Postgres;
using HexMaster.ThePrey.Games.DomainModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HexMaster.ThePrey.GameEngine;

internal sealed class GameLocationChecker
{
    private readonly IDbContextFactory<GamesDbContext> _dbContextFactory;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IGameEngineMetrics _metrics;
    private readonly ILogger<GameLocationChecker> _logger;

    private readonly Dictionary<Guid, DateTimeOffset> _lastBroadcastTimes = new();
    private int _cycleNumber;

    // Must be public: it is resolved from the DI container via AddSingleton<GameLocationChecker>(),
    // and Microsoft.Extensions.DependencyInjection only selects public constructors.
    public GameLocationChecker(
        IDbContextFactory<GamesDbContext> dbContextFactory,
        IHttpClientFactory httpClientFactory,
        IGameEngineMetrics metrics,
        ILogger<GameLocationChecker> logger)
    {
        _dbContextFactory = dbContextFactory;
        _httpClientFactory = httpClientFactory;
        _metrics = metrics;
        _logger = logger;
    }

    /// <summary>
    /// Computes the next aligned tick relative to <paramref name="startTime"/>.
    /// Ticks are evenly spaced at <paramref name="intervalSeconds"/> from startTime.
    /// Always advances at least one full interval from the current moment.
    /// </summary>
    internal static DateTimeOffset ComputeNextTick(DateTimeOffset startTime, DateTimeOffset now, int intervalSeconds)
    {
        var elapsed = (now - startTime).TotalSeconds;
        var tickNumber = Math.Floor(elapsed / intervalSeconds) + 1;
        return startTime.AddSeconds(tickNumber * intervalSeconds);
    }

    internal async Task RunAsync(Guid gameId, CancellationToken ct)
    {
        await using var initialContext = await _dbContextFactory.CreateDbContextAsync(ct);
        var game = await initialContext.Games.FirstOrDefaultAsync(g => g.Id == gameId, ct);

        if (game is null)
        {
            _logger.LogWarning("Game {GameId} not found; engine exiting", gameId);
            return;
        }

        if (game.StartedAt is not { } startTime)
        {
            _logger.LogWarning("Game {GameId} has no start time; engine exiting", gameId);
            return;
        }

        _logger.LogInformation("Game engine starting for game {GameId}", gameId);

        while (!ct.IsCancellationRequested)
        {
            var now = DateTimeOffset.UtcNow;

            await using var cycleContext = await _dbContextFactory.CreateDbContextAsync(ct);
            var currentGame = await cycleContext.Games.FirstOrDefaultAsync(g => g.Id == gameId, ct);

            if (currentGame is null || currentGame.Status != GameStatus.InProgress)
            {
                _logger.LogInformation("Game {GameId} is no longer in progress; engine exiting", gameId);
                return;
            }

            if (currentGame.ScheduledEndAt is { } end && now >= end)
            {
                _logger.LogInformation("Game {GameId} has ended; performing final broadcast", gameId);
                await BroadcastAllParticipantsAsync(cycleContext, currentGame, ct);
                await CallCompleteGameEndpointAsync(gameId, ct);
                return;
            }

            var intervalSeconds = currentGame.RegularReportingIntervalAt(now);
            var nextTick = ComputeNextTick(startTime, now, intervalSeconds);
            var delay = nextTick - DateTimeOffset.UtcNow;

            if (delay > TimeSpan.Zero)
                await Task.Delay(delay, ct);

            if (ct.IsCancellationRequested) break;

            await using var execContext = await _dbContextFactory.CreateDbContextAsync(ct);
            var execGame = await execContext.Games.FirstOrDefaultAsync(g => g.Id == gameId, ct);

            if (execGame is null || execGame.Status != GameStatus.InProgress) return;

            await ExecuteCycleAsync(execContext, execGame, ct);
        }
    }

    private async Task ExecuteCycleAsync(GamesDbContext dbContext, Game game, CancellationToken ct)
    {
        using var activity = GameEngineActivitySource.Source.StartActivity("GameEngine.ExecuteCycle");
        activity?.SetTag("game.id", game.Id);
        activity?.SetTag("game.cycle_number", ++_cycleNumber);

        try
        {
            var now = DateTimeOffset.UtcNow;
            var eligibleParticipants = DetermineEligibleParticipants(game, now);

            if (eligibleParticipants.Count == 0)
            {
                _metrics.RecordCycleExecuted(game.Id);
                return;
            }

            var locationUpdates = BuildLocationUpdates([.. eligibleParticipants], now);

            if (locationUpdates.Count > 0)
            {
                await dbContext.SaveChangesAsync(ct);
                await CallLocationUpdateEndpointAsync(game.Id, locationUpdates, ct);
            }

            _metrics.RecordCycleExecuted(game.Id);
            _metrics.RecordLocationsBroadcasted(locationUpdates.Count, game.Id);

            _logger.LogDebug("Game {GameId} cycle {Cycle}: broadcasted {Count} locations",
                game.Id, _cycleNumber, locationUpdates.Count);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            _logger.LogError(ex, "Error executing cycle {Cycle} for game {GameId}", _cycleNumber, game.Id);
            throw;
        }
    }

    private IReadOnlyList<GameParticipant> DetermineEligibleParticipants(Game game, DateTimeOffset now) =>
        EligibilityEvaluator.GetEligible(game, now, _lastBroadcastTimes);

    private List<ParticipantLocationPayload> BuildLocationUpdates(
        List<GameParticipant> participants,
        DateTimeOffset now)
    {
        var updates = new List<ParticipantLocationPayload>();

        foreach (var participant in participants)
        {
            var mostRecent = participant.Locations
                .OrderByDescending(l => l.RecordedAt)
                .FirstOrDefault();

            if (mostRecent is null) continue;

            participant.UpdateBroadcastLocation(mostRecent.Coordinate);
            updates.Add(new ParticipantLocationPayload(
                participant.UserId,
                mostRecent.Coordinate.Latitude,
                mostRecent.Coordinate.Longitude));

            _lastBroadcastTimes[participant.UserId] = now;
        }

        return updates;
    }

    private async Task BroadcastAllParticipantsAsync(GamesDbContext dbContext, Game game, CancellationToken ct)
    {
        using var activity = GameEngineActivitySource.Source.StartActivity("GameEngine.FinalBroadcast");
        activity?.SetTag("game.id", game.Id);

        try
        {
            var allParticipants = game.Participants.ToList();

            var locationUpdates = new List<ParticipantLocationPayload>();
            foreach (var participant in allParticipants)
            {
                var mostRecent = participant.Locations
                    .OrderByDescending(l => l.RecordedAt)
                    .FirstOrDefault();

                if (mostRecent is null) continue;

                participant.UpdateBroadcastLocation(mostRecent.Coordinate);
                locationUpdates.Add(new ParticipantLocationPayload(
                    participant.UserId,
                    mostRecent.Coordinate.Latitude,
                    mostRecent.Coordinate.Longitude));
            }

            if (locationUpdates.Count > 0)
            {
                await dbContext.SaveChangesAsync(ct);
                await CallLocationUpdateEndpointAsync(game.Id, locationUpdates, ct);
            }

            _logger.LogInformation("Final broadcast complete for game {GameId}: {Count} participants",
                game.Id, locationUpdates.Count);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            _logger.LogError(ex, "Error during final broadcast for game {GameId}", game.Id);
            throw;
        }
    }

    private async Task CallLocationUpdateEndpointAsync(
        Guid gameId,
        List<ParticipantLocationPayload> locations,
        CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient("GamesApi");
        var payload = new { Locations = locations };
        var response = await client.PostAsJsonAsync($"/game-engine/{gameId}/location-update", payload, ct);
        response.EnsureSuccessStatusCode();
    }

    private async Task CallCompleteGameEndpointAsync(Guid gameId, CancellationToken ct)
    {
        using var activity = GameEngineActivitySource.Source.StartActivity("GameEngine.CompleteGame");
        activity?.SetTag("game.id", gameId);

        try
        {
            var client = _httpClientFactory.CreateClient("GamesApi");
            // Empty body — the endpoint only needs the gameId in the route.
            var response = await client.PostAsJsonAsync($"/game-engine/{gameId}/complete", new { }, ct);
            response.EnsureSuccessStatusCode();
            _logger.LogInformation("Game {GameId} completion notification sent successfully", gameId);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            _logger.LogError(ex, "Failed to notify Games API of completion for game {GameId}", gameId);
            throw;
        }
    }

    private sealed record ParticipantLocationPayload(Guid UserId, double Latitude, double Longitude);
}
