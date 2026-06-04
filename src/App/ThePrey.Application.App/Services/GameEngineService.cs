using System.Net.Http;
using Microsoft.Extensions.Logging;
using ThePrey.Application.App.Models;

namespace ThePrey.Application.App.Services;

/// <summary>
/// Runs the game session's background loops: a location-push loop at the server-controlled
/// interval (with penalty overrides) and a fixed-interval game-state sync loop. All observable
/// results are written to <see cref="GameStateContext"/>.
/// </summary>
public sealed class GameEngineService(
    IGameService gameService,
    GameStateContext state,
    ILogger<GameEngineService> logger) : IGameEngineService
{
    /// <summary>Push interval used until the first successful server response.</summary>
    private const int BootstrapPushIntervalSeconds = 10;
    private const int MaxPushAttempts = 3;
    private static readonly TimeSpan StateSyncInterval = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan GpsFixTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan TransientRetryDelay = TimeSpan.FromSeconds(5);

    private readonly SemaphoreSlim _lifecycleLock = new(1, 1);

    private CancellationTokenSource? _cts;
    private Task? _loops;
    private string? _gameId;
    private PlayerRole _role;
    private bool _sessionActive;

    private int _regularIntervalSeconds = BootstrapPushIntervalSeconds;
    private int? _penaltyIntervalSeconds;
    private DateTimeOffset? _penaltyEndsAt;

    private bool LoopsActive => _cts is { IsCancellationRequested: false };

    public async Task StartAsync(string gameId, PlayerRole role)
    {
        await _lifecycleLock.WaitAsync();
        try
        {
            if (LoopsActive)
                return; // already running — idempotent

            await DrainLoopsAsync();

            _gameId = gameId;
            _role = role;
            _sessionActive = true;
            _regularIntervalSeconds = BootstrapPushIntervalSeconds;
            _penaltyIntervalSeconds = null;
            _penaltyEndsAt = null;
            state.Reset(role);

            StartLoops();
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    public async Task StopAsync()
    {
        await _lifecycleLock.WaitAsync();
        try
        {
            _sessionActive = false;
            _cts?.Cancel();
            await DrainLoopsAsync();
            state.IsRunning = false;
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    public async Task SuspendAsync()
    {
        await _lifecycleLock.WaitAsync();
        try
        {
            _cts?.Cancel();
            await DrainLoopsAsync();
            state.IsRunning = false;
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    public async Task ResumeAsync()
    {
        await _lifecycleLock.WaitAsync();
        try
        {
            if (_sessionActive && !LoopsActive)
            {
                await DrainLoopsAsync();
                StartLoops();
            }
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    private void StartLoops()
    {
        var cts = new CancellationTokenSource();
        _cts = cts;
        _loops = Task.WhenAll(
            Task.Run(() => PushLoopAsync(cts.Token), CancellationToken.None),
            Task.Run(() => SyncLoopAsync(cts.Token), CancellationToken.None));
        state.IsRunning = true;
    }

    /// <summary>Awaits any previously started loops and releases their cancellation source.</summary>
    private async Task DrainLoopsAsync()
    {
        if (_loops is { } loops)
        {
            try
            {
                await loops;
            }
            catch (OperationCanceledException)
            {
                // expected on suspension/stop
            }
        }

        _cts?.Dispose();
        _cts = null;
        _loops = null;
    }

    /// <summary>
    /// Stops the engine from inside a loop (game ended or session lost). Only cancels and flags —
    /// it must not await the loops or take the lifecycle lock, both would deadlock the caller.
    /// </summary>
    private void StopFromLoop()
    {
        _sessionActive = false;
        state.IsRunning = false;
        _cts?.Cancel();
    }

    private async Task PushLoopAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(BootstrapPushIntervalSeconds));
        try
        {
            while (!ct.IsCancellationRequested)
            {
                await PushLocationOnceAsync(ct);
                timer.Period = TimeSpan.FromSeconds(EffectivePushIntervalSeconds(DateTimeOffset.UtcNow));
                if (!await timer.WaitForNextTickAsync(ct))
                    break;
            }
        }
        catch (OperationCanceledException)
        {
            // loop suspended or stopped
        }
    }

    private async Task PushLocationOnceAsync(CancellationToken ct)
    {
        var location = await AcquireLocationAsync(ct);
        if (location is null)
        {
            state.GpsAvailable = false;
            return;
        }

        state.GpsAvailable = true;

        for (var attempt = 1; attempt <= MaxPushAttempts; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var response = await gameService.PushLocationAsync(
                    _gameId!, location.Latitude, location.Longitude, location.Accuracy, ct);

                if (response is null)
                {
                    state.GameEnded = true;
                    StopFromLoop();
                    return;
                }

                ApplyIntervalDirective(response);
                state.LastLocationPushedAt = DateTimeOffset.UtcNow;
                state.ConsecutiveErrors = 0;
                return;
            }
            catch (UnauthorizedException)
            {
                logger.LogWarning("Session could not be recovered; stopping the game engine.");
                StopFromLoop();
                return;
            }
            catch (Exception ex) when (IsTransient(ex, ct))
            {
                logger.LogWarning(ex, "Location push attempt {Attempt}/{MaxAttempts} failed.", attempt, MaxPushAttempts);
                if (attempt < MaxPushAttempts)
                    await Task.Delay(TransientRetryDelay, ct);
            }
        }

        state.ConsecutiveErrors++;
    }

    /// <summary>
    /// Acquires a GPS fix at medium accuracy within <see cref="GpsFixTimeout"/>, falling back to
    /// the last known location. Returns null when no fix (current or cached) is available.
    /// </summary>
    private async Task<Location?> AcquireLocationAsync(CancellationToken ct)
    {
        try
        {
            var request = new GeolocationRequest(GeolocationAccuracy.Medium, GpsFixTimeout);
            var location = await Geolocation.Default.GetLocationAsync(request, ct);
            if (location is not null)
                return location;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Fresh GPS fix failed; falling back to the last known location.");
        }

        try
        {
            var lastKnown = await Geolocation.Default.GetLastKnownLocationAsync();
            if (lastKnown is not null)
                logger.LogWarning("Fresh GPS fix unavailable; pushing the last known location.");
            return lastKnown;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Last known location unavailable.");
            return null;
        }
    }

    /// <summary>Adopts the server's interval directive: regular interval plus an optional penalty override.</summary>
    private void ApplyIntervalDirective(LocationPushResponse response)
    {
        // Absent or zero → retain the previously active interval.
        if (response.NextLocationIntervalSeconds > 0)
            _regularIntervalSeconds = response.NextLocationIntervalSeconds;

        if (response.PenaltyIntervalSeconds is > 0 && response.PenaltyEndsAt is { } endsAt && endsAt > DateTimeOffset.UtcNow)
        {
            _penaltyIntervalSeconds = response.PenaltyIntervalSeconds;
            _penaltyEndsAt = endsAt;
        }
        else
        {
            _penaltyIntervalSeconds = null;
            _penaltyEndsAt = null;
        }
    }

    /// <summary>
    /// The push interval to apply right now: the penalty override while it is active, otherwise
    /// the regular server-controlled interval. Also keeps the context's penalty state current.
    /// </summary>
    private int EffectivePushIntervalSeconds(DateTimeOffset now)
    {
        if (_penaltyIntervalSeconds is { } penaltyInterval && _penaltyEndsAt is { } endsAt && endsAt > now)
        {
            state.IsUnderPenalty = true;
            state.PenaltyEndsAt = endsAt;
            return penaltyInterval;
        }

        state.IsUnderPenalty = false;
        state.PenaltyEndsAt = null;
        return _regularIntervalSeconds;
    }

    private async Task SyncLoopAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(StateSyncInterval);
        try
        {
            // Sync once right away so the UI has state immediately after start/resume.
            await SyncGameStateOnceAsync(ct);
            while (await timer.WaitForNextTickAsync(ct))
                await SyncGameStateOnceAsync(ct);
        }
        catch (OperationCanceledException)
        {
            // loop suspended or stopped
        }
    }

    private async Task SyncGameStateOnceAsync(CancellationToken ct)
    {
        try
        {
            var snapshot = await gameService.GetGameStateAsync(_gameId!, ct);
            if (snapshot is null)
            {
                state.GameEnded = true;
                StopFromLoop();
                return;
            }

            // Only the role-relevant field is updated; the other is left untouched.
            if (_role == PlayerRole.Prey)
                state.HunterDistanceMeters = snapshot.HunterDistanceMeters;
            else
                state.ReplacePreyLocations(snapshot.PreyLocations);

            state.LastStateSyncAt = DateTimeOffset.UtcNow;
        }
        catch (UnauthorizedException)
        {
            logger.LogWarning("Session could not be recovered; stopping the game engine.");
            StopFromLoop();
        }
        catch (Exception ex) when (IsTransient(ex, ct))
        {
            logger.LogWarning(ex, "Game state sync failed; retrying at the next interval.");
        }
    }

    /// <summary>True for 5xx responses, network failures, and HTTP timeouts (not user cancellation).</summary>
    private static bool IsTransient(Exception ex, CancellationToken ct) =>
        ex switch
        {
            HttpRequestException { StatusCode: null } => true,
            HttpRequestException { StatusCode: { } status } => (int)status >= 500,
            TaskCanceledException => !ct.IsCancellationRequested,
            _ => false
        };
}
