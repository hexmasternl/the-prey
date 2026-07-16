using HexMaster.ThePrey.Maui.App.Services.Authentication;
using Microsoft.Extensions.Logging;

namespace HexMaster.ThePrey.Maui.App.Services.Location;

/// <summary>
/// The platform-neutral heart of background location tracking. Owns the cadence loop, token acquisition,
/// HTTP reporting, server-driven cadence adoption, retry, and the stop conditions. Platform code is
/// confined to two thin adapters — <see cref="IBackgroundExecutionHost"/> (keeps the process alive) and
/// <see cref="IContinuousLocationSource"/> (supplies fixes) — so all testable logic lives here.
///
/// <para>Cadence seeds at a 10-second default and, after each accepted report, adopts the server's
/// <c>NextLocationIntervalSeconds</c> (or the penalty interval when active), clamped to a 5-second
/// minimum so a zero/negative value can never busy-loop. Transient GPS/network/5xx failures and missing
/// fixes keep tracking; only an explicit stop or a game-over signal (404/422) ends it.</para>
///
/// <para>Start reports the first fix inline (so a just-started game is located immediately) and then
/// launches a background loop for subsequent ticks; the loop is driven by <see cref="TimeProvider"/> so
/// it is deterministic under test.</para>
/// </summary>
public sealed class GameLocationTrackerCoordinator
{
    internal static readonly TimeSpan DefaultInterval = TimeSpan.FromSeconds(10);
    internal static readonly TimeSpan MinInterval = TimeSpan.FromSeconds(5);

    private readonly IContinuousLocationSource _source;
    private readonly IBackgroundExecutionHost _host;
    private readonly ILocationReportClient _reportClient;
    private readonly IAccessTokenProvider _accessTokenProvider;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<GameLocationTrackerCoordinator> _logger;

    // Guards Start/Stop transitions so two callers can't half-start/half-stop concurrently.
    private readonly SemaphoreSlim _gate = new(1, 1);

    private Guid? _trackingGameId;
    private TimeSpan _currentInterval = DefaultInterval;
    private CancellationTokenSource? _loopCts;
    private Task? _loopTask;

    public GameLocationTrackerCoordinator(
        IContinuousLocationSource source,
        IBackgroundExecutionHost host,
        ILocationReportClient reportClient,
        IAccessTokenProvider accessTokenProvider,
        TimeProvider timeProvider,
        ILogger<GameLocationTrackerCoordinator> logger)
    {
        _source = source;
        _host = host;
        _reportClient = reportClient;
        _accessTokenProvider = accessTokenProvider;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <summary>True while a game is being tracked. Test/diagnostic seam.</summary>
    internal bool IsTracking => _trackingGameId is not null;

    /// <summary>The cadence that will govern the next tick. Test/diagnostic seam.</summary>
    internal TimeSpan CurrentInterval => _currentInterval;

    /// <summary>
    /// Begins tracking <paramref name="gameId"/>. No-op if already tracking the same game. If already
    /// tracking a different game, the previous loop is stopped first. Reports the first fix inline; if
    /// that first report already says the game is over, tracking never starts. Never throws.
    /// </summary>
    public async Task StartAsync(Guid gameId, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            if (_trackingGameId == gameId)
                return; // Already tracking this game — idempotent no-op.

            if (_trackingGameId is not null)
                await StopTrackingLoopAsync(); // Switching games — retire the previous loop.

            _currentInterval = DefaultInterval;
            _trackingGameId = gameId;

            await SafeAsync(() => _source.StartAsync(ct), "start the location source");
            await SafeAsync(() => _host.StartAsync(ct), "start the background-execution host");

            // Report the first fix inline so the server locates the player as soon as the game starts.
            var kind = await ExecuteTickAsync(gameId, ct);
            if (kind == TickKind.GameOver)
            {
                await TearDownAsync();
                _trackingGameId = null;
                return;
            }

            _loopCts = new CancellationTokenSource();
            _loopTask = Task.Run(() => RunLoopAsync(gameId, _loopCts.Token), CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
            // Start was cancelled — leave nothing running.
            await TearDownAsync();
            _trackingGameId = null;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Stops tracking and releases all background-execution resources. No-op when not tracking.</summary>
    public async Task StopAsync()
    {
        await _gate.WaitAsync();
        try
        {
            if (_trackingGameId is null)
                return; // Not tracking — idempotent no-op.

            await StopTrackingLoopAsync();
            _trackingGameId = null;
        }
        finally
        {
            _gate.Release();
        }
    }

    // Cancels and awaits the background loop (if any), then tears down the native adapters. The loop
    // body never takes _gate, so awaiting it here (under the gate) cannot deadlock.
    private async Task StopTrackingLoopAsync()
    {
        _loopCts?.Cancel();

        var loop = _loopTask;
        _loopTask = null;
        if (loop is not null)
        {
            try { await loop; }
            catch (Exception ex) { _logger.LogInformation(ex, "Location tracking loop ended with an error."); }
        }

        _loopCts?.Dispose();
        _loopCts = null;

        await TearDownAsync();
    }

    private async Task RunLoopAsync(Guid gameId, CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                // The first fix was already reported by StartAsync; wait one cadence before the next.
                await Task.Delay(_currentInterval, _timeProvider, ct);

                var kind = await ExecuteTickAsync(gameId, ct);
                if (kind == TickKind.GameOver)
                {
                    // Defensive stop: the endpoint says the game is no longer InProgress. Tear the
                    // adapters down here; an external StopAsync remains a safe no-op afterwards.
                    await TearDownAsync();
                    ClearTrackingIfCurrent(gameId);
                    return;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Cancelled by StopAsync — expected; teardown is handled by the caller.
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Location tracking loop faulted; tracking has stopped.");
            await TearDownAsync();
            ClearTrackingIfCurrent(gameId);
        }
    }

    // Performs one tick: acquire a fix, acquire/refresh a token, report, and adopt the next cadence.
    // Never throws — every failure maps to a TickKind the loop can act on.
    internal async Task<TickKind> ExecuteTickAsync(Guid gameId, CancellationToken ct)
    {
        LocationSample? sample;
        try
        {
            sample = await _source.GetCurrentAsync(ct);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogInformation(ex, "Acquiring a location fix failed — skipping this tick.");
            return TickKind.Skipped;
        }

        if (sample is null)
            return TickKind.Skipped; // No fix this tick — keep tracking, try again next tick.

        var token = await _accessTokenProvider.GetAccessTokenAsync(ct);
        if (token is null)
            return TickKind.Transient; // Can't authenticate right now — retry next tick.

        var request = new RecordLocationRequest(sample.Latitude, sample.Longitude, sample.RecordedAt, sample.Accuracy);
        var result = await _reportClient.ReportAsync(gameId, request, token, ct);
        switch (result.Outcome)
        {
            case LocationReportOutcome.Accepted:
                if (result.Response is not null)
                    _currentInterval = AdoptInterval(result.Response);
                return TickKind.Reported;

            case LocationReportOutcome.Unauthorized:
                // The token was rejected mid-game — drop the cache so the next tick re-exchanges.
                _accessTokenProvider.Invalidate();
                return TickKind.Transient;

            case LocationReportOutcome.GameOver:
                return TickKind.GameOver;

            default:
                return TickKind.Transient;
        }
    }

    // The server owns the cadence. Prefer the penalty interval when one is active, else the regular
    // interval; clamp to the minimum so a zero/negative/tiny value can never produce a busy loop.
    internal static TimeSpan AdoptInterval(RecordLocationResponse response)
    {
        var seconds = response.PenaltyIntervalSeconds is int penalty && penalty > 0
            ? penalty
            : response.NextLocationIntervalSeconds;

        if (seconds <= 0)
            return MinInterval;

        var interval = TimeSpan.FromSeconds(seconds);
        return interval < MinInterval ? MinInterval : interval;
    }

    private void ClearTrackingIfCurrent(Guid gameId)
    {
        if (_trackingGameId == gameId)
            _trackingGameId = null;
    }

    private async Task TearDownAsync()
    {
        await SafeAsync(_host.StopAsync, "stop the background-execution host");
        await SafeAsync(_source.StopAsync, "stop the location source");
    }

    private async Task SafeAsync(Func<Task> action, string what)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to {What}.", what);
        }
    }

    /// <summary>The outcome of a single cadence tick.</summary>
    internal enum TickKind
    {
        /// <summary>A fix was reported and the next cadence adopted.</summary>
        Reported,

        /// <summary>No fix available this tick — skipped, still tracking.</summary>
        Skipped,

        /// <summary>Transient network/auth/5xx failure — retry next tick, still tracking.</summary>
        Transient,

        /// <summary>The game is no longer InProgress — stop tracking.</summary>
        GameOver
    }
}
