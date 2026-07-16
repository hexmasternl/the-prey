using System.Text.Json;
using HexMaster.ThePrey.Maui.App.Services.Api;
using HexMaster.ThePrey.Maui.App.Services.Authentication;
using Microsoft.Extensions.Logging;

namespace HexMaster.ThePrey.Maui.App.Services.Realtime;

/// <summary>
/// Default <see cref="IGameStateService"/> — the single in-game store. Subscribes to an
/// <see cref="IGameRealtimeConnection"/>: each real-time envelope is applied to the in-memory
/// <see cref="GameLiveState"/> composite, and every (re)connect plus a periodic heartbeat triggers a full
/// reconcile (game record + rich status + role-specific state) so gaps while the socket was down — and any
/// values the channel never pushes, like the game clock — are healed. Every change is broadcast to
/// subscribers, each isolated so one failure cannot starve the others. The service is UI-agnostic; callers
/// marshal to the UI thread at the subscription boundary.
/// </summary>
public sealed class GameStateService : IGameStateService
{
    /// <summary>Heartbeat cadence for the safety-net reconcile that heals silent drift.</summary>
    private static readonly TimeSpan ReconcileInterval = TimeSpan.FromMinutes(5);

    private static readonly JsonSerializerOptions PayloadOptions = new(JsonSerializerDefaults.Web);

    private readonly IGameRealtimeConnection _connection;
    private readonly IGameApiClient _gameApi;
    private readonly IAccessTokenProvider _accessTokenProvider;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<GameStateService> _logger;

    private readonly object _stateGate = new();
    private readonly object _subscriberGate = new();
    private readonly List<Action<GameStateChanged>> _subscribers = new();

    private GameLiveState? _current;
    private Guid _gameId;
    private ITimer? _reconcileTimer;

    public GameStateService(
        IGameRealtimeConnection connection,
        IGameApiClient gameApi,
        IAccessTokenProvider accessTokenProvider,
        TimeProvider timeProvider,
        ILogger<GameStateService> logger)
    {
        _connection = connection;
        _gameApi = gameApi;
        _accessTokenProvider = accessTokenProvider;
        _timeProvider = timeProvider;
        _logger = logger;

        _connection.EnvelopeReceived += OnEnvelopeReceived;
        _connection.Connected += OnConnectedOrReconnected;
        _connection.Reconnected += OnConnectedOrReconnected;
        _connection.Unavailable += OnUnavailable;
    }

    public GameLiveState? CurrentState
    {
        get { lock (_stateGate) { return _current; } }
    }

    public async Task<GameLiveState?> StartAsync(CancellationToken ct = default)
    {
        var token = await _accessTokenProvider.GetAccessTokenAsync(ct);
        if (string.IsNullOrWhiteSpace(token))
        {
            _logger.LogWarning("Cannot start game state: no access token available.");
            return null;
        }

        var active = await _gameApi.GetActiveGameAsync(token!, ct);
        if (active.Outcome != ActiveGameOutcome.HasActiveGame || active.Game is null)
        {
            if (active.Outcome == ActiveGameOutcome.Unauthorized)
                _accessTokenProvider.Invalidate();
            _logger.LogInformation("Cannot start game state: active-game lookup returned {Outcome}.", active.Outcome);
            return null;
        }

        _gameId = active.Game.GameId;

        // Seed an immediate snapshot so the map/HUD render without waiting for the socket to connect,
        // then go live. The connection's Connected reconcile refreshes it again shortly after.
        await ReconcileAsync(ct);
        StartPeriodicReconcile();
        _connection.Start(_gameId);
        return CurrentState;
    }

    public void Start(Guid gameId)
    {
        _gameId = gameId;
        StartPeriodicReconcile();
        _connection.Start(gameId);
    }

    public async Task StopAsync()
    {
        _reconcileTimer?.Dispose();
        _reconcileTimer = null;
        await _connection.StopAsync();
    }

    public void Subscribe(Action<GameStateChanged> handler)
    {
        lock (_subscriberGate) { _subscribers.Add(handler); }
    }

    public void Unsubscribe(Action<GameStateChanged> handler)
    {
        lock (_subscriberGate) { _subscribers.Remove(handler); }
    }

    private void StartPeriodicReconcile()
    {
        _reconcileTimer?.Dispose();
        _reconcileTimer = _timeProvider.CreateTimer(
            _ => _ = SafeReconcileAsync(), null, ReconcileInterval, ReconcileInterval);
    }

    // On (re)connect, pull the authoritative snapshot so any events missed while the socket was down are
    // reconciled. Fire-and-forget: the connection's event is synchronous and must not block its loop.
    private void OnConnectedOrReconnected() => _ = SafeReconcileAsync();

    private async Task SafeReconcileAsync()
    {
        try
        {
            await ReconcileAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Game-state reconcile for {GameId} failed.", _gameId);
        }
    }

    // Rebuilds the whole composite from the authoritative reads: the game record always, plus the rich
    // in-progress status and role-specific state while the game is actually running. Anything the reads
    // omit (locations between fixes, an active penalty) is carried over from the previous snapshot.
    private async Task ReconcileAsync(CancellationToken ct = default)
    {
        var token = await _accessTokenProvider.GetAccessTokenAsync(ct);
        if (string.IsNullOrWhiteSpace(token))
        {
            _logger.LogWarning("Cannot reconcile game state: no access token available.");
            return;
        }

        var gameResult = await _gameApi.GetGameAsync(_gameId, token!, ct);
        if (gameResult.Outcome != GetGameOutcome.Success || gameResult.Game is null)
        {
            if (gameResult.Outcome == GetGameOutcome.Unauthorized)
                _accessTokenProvider.Invalidate();
            _logger.LogWarning("Game-state reconcile for {GameId} returned {Outcome}; keeping last state.", _gameId, gameResult.Outcome);
            return;
        }

        var game = gameResult.Game;
        GameStatusDetails? details = null;
        GameStateSnapshot? state = null;

        if (IsInProgress(game.Status))
        {
            var statusResult = await _gameApi.GetGameStatusDetailsAsync(_gameId, token!, ct);
            if (statusResult.Outcome == GetGameStatusOutcome.Success)
                details = statusResult.Details;

            var stateResult = await _gameApi.GetGameStateAsync(_gameId, token!, ct);
            if (stateResult.Outcome == GameStateOutcome.Success)
                state = stateResult.State;
        }

        GameLiveState built;
        lock (_stateGate)
        {
            built = BuildState(game, details, state, _current);
            _current = built;
        }
        Broadcast(built);
    }

    private void OnUnavailable()
    {
        _logger.LogWarning("Game real-time channel reported unavailable for {GameId}.", _gameId);
    }

    private void OnEnvelopeReceived(GameRealtimeEnvelope envelope)
    {
        var updated = ApplyEnvelope(envelope);
        if (updated is not null)
            Broadcast(updated);
    }

    // Applies one event to the current snapshot under the state lock. Returns the new snapshot when the
    // state changed, or null when the event was a no-op (unknown type, malformed payload, missing target).
    private GameLiveState? ApplyEnvelope(GameRealtimeEnvelope envelope)
    {
        if (string.IsNullOrWhiteSpace(envelope.Type))
            return null;

        lock (_stateGate)
        {
            if (GameRealtimeEventTypes.FullSnapshotEvents.Contains(envelope.Type))
            {
                var game = Deserialize<GameDetails>(envelope.Data);
                if (game is null)
                    return null;
                // A full-snapshot event (lobby/game-started) carries only the game record; merge it onto the
                // previous composite so the map's polygon and the participants' last-known locations survive.
                _current = BuildState(game, details: null, state: null, _current);
                return _current;
            }

            var current = _current;
            switch (envelope.Type)
            {
                case GameRealtimeEventTypes.StateChanged:
                {
                    if (current is null)
                        return null;
                    var payload = Deserialize<StateChangedPayload>(envelope.Data);
                    if (payload is null || string.IsNullOrEmpty(payload.NewState))
                        return null;
                    _current = current with { Status = payload.NewState };
                    return _current;
                }

                case GameRealtimeEventTypes.PlayerLocationUpdated:
                {
                    if (current is null)
                        return null;
                    var payload = Deserialize<PlayerLocationUpdatedPayload>(envelope.Data);
                    if (payload is null)
                        return null;
                    var participants = UpdateParticipant(current.Participants, payload.UserId, p => p with
                    {
                        Location = new GpsCoordinate(payload.Latitude, payload.Longitude),
                        State = string.IsNullOrEmpty(payload.ParticipantState) ? p.State : payload.ParticipantState,
                    });
                    if (participants is null)
                        return null;
                    _current = current with
                    {
                        Participants = participants,
                        PreysLeft = CountActivePreys(participants, current.HunterUserId),
                    };
                    return _current;
                }

                case GameRealtimeEventTypes.ParticipantStatusChanged:
                {
                    if (current is null)
                        return null;
                    var payload = Deserialize<ParticipantStatusChangedPayload>(envelope.Data);
                    if (payload is null || string.IsNullOrEmpty(payload.NewState))
                        return null;
                    var participants = UpdateParticipant(current.Participants, payload.ParticipantId,
                        p => p with { State = payload.NewState });
                    if (participants is null)
                        return null;
                    _current = current with
                    {
                        Participants = participants,
                        PreysLeft = CountActivePreys(participants, current.HunterUserId),
                    };
                    return _current;
                }

                case GameRealtimeEventTypes.PlayerPenalized:
                {
                    if (current is null)
                        return null;
                    var payload = Deserialize<PlayerPenalizedPayload>(envelope.Data);
                    if (payload is null)
                        return null;
                    var participants = UpdateParticipant(current.Participants, payload.UserId,
                        p => p with { PenaltyEndsAt = payload.PenaltyEndsAt });
                    if (participants is null)
                        return null;
                    _current = current with { Participants = participants };
                    return _current;
                }

                case GameRealtimeEventTypes.GameEnded:
                {
                    if (current is null)
                        return null;
                    _current = current with { Status = "Completed" };
                    return _current;
                }

                default:
                    _logger.LogDebug("Ignoring unhandled game real-time event '{Type}'.", envelope.Type);
                    return null;
            }
        }
    }

    // Merges the authoritative reads into a fresh composite, overlaying the previous snapshot for anything
    // the reads omit: the polygon (static — kept if a read lacked it), and each participant's last-known
    // location and active penalty (the /status read carries locations but never penalties).
    private static GameLiveState BuildState(
        GameDetails game, GameStatusDetails? details, GameStateSnapshot? state, GameLiveState? previous)
    {
        var previousById = previous?.Participants.ToDictionary(p => p.UserId) ?? new Dictionary<Guid, GameLiveParticipant>();
        var hunterUserId = game.HunterUserId ?? details?.HunterUserId ?? previous?.HunterUserId;

        List<GameLiveParticipant> participants;
        if (details is not null)
        {
            participants = details.Participants.Select(p =>
            {
                previousById.TryGetValue(p.UserId, out var prev);
                return new GameLiveParticipant(
                    p.UserId,
                    p.State,
                    p.LastKnownLocation ?? prev?.Location,
                    prev?.PenaltyEndsAt);
            }).ToList();
        }
        else
        {
            // No in-progress status read (lobby/ready, or a full-snapshot event): use the game roster and
            // preserve any location/penalty already known so the map does not blink out.
            participants = game.Participants.Select(p =>
            {
                previousById.TryGetValue(p.UserId, out var prev);
                return new GameLiveParticipant(p.UserId, p.State, prev?.Location, prev?.PenaltyEndsAt);
            }).ToList();
        }

        return new GameLiveState
        {
            GameId = game.Id,
            Status = game.Status,
            HunterUserId = hunterUserId,
            Participants = participants,
            PlayfieldCoordinates = details?.PlayfieldCoordinates.Count > 0
                ? details.PlayfieldCoordinates
                : previous?.PlayfieldCoordinates ?? [],
            HunterMayMoveAt = details?.HunterMayMoveAt ?? previous?.HunterMayMoveAt,
            GameDurationLeft = details?.GameDurationLeft ?? previous?.GameDurationLeft ?? 0,
            NextPingDuration = details?.NextPingDuration ?? previous?.NextPingDuration ?? 0,
            CurrentPingInterval = details?.CurrentPingInterval ?? previous?.CurrentPingInterval ?? 0,
            IsEndgame = details?.IsEndgame ?? previous?.IsEndgame ?? false,
            PreysLeft = details?.PreysLeft ?? CountActivePreys(participants, hunterUserId),
            HunterDistanceMeters = state?.HunterDistanceMeters ?? previous?.HunterDistanceMeters,
            PreyLocations = state?.PreyLocations ?? previous?.PreyLocations ?? [],
        };
    }

    private void Broadcast(GameLiveState state)
    {
        Action<GameStateChanged>[] handlers;
        lock (_subscriberGate) { handlers = _subscribers.ToArray(); }

        var message = new GameStateChanged(state);
        foreach (var handler in handlers)
        {
            try
            {
                handler(message);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "A game-state subscriber threw; other subscribers are unaffected.");
            }
        }
    }

    private static bool IsInProgress(string status) =>
        string.Equals(status, "InProgress", StringComparison.OrdinalIgnoreCase);

    // Active preys = participants that are not the hunter and are still in play (Active/Passive).
    private static int CountActivePreys(IReadOnlyList<GameLiveParticipant> participants, Guid? hunterUserId)
    {
        var count = 0;
        foreach (var p in participants)
        {
            if (hunterUserId is { } hunter && p.UserId == hunter)
                continue;
            if (string.Equals(p.State, "Active", StringComparison.OrdinalIgnoreCase)
                || string.Equals(p.State, "Passive", StringComparison.OrdinalIgnoreCase))
                count++;
        }
        return count;
    }

    // Returns a new participant list with the matching participant transformed, or null when no
    // participant matches (so the caller can treat it as a no-op instead of a spurious change).
    private static IReadOnlyList<GameLiveParticipant>? UpdateParticipant(
        IReadOnlyList<GameLiveParticipant> participants,
        Guid userId,
        Func<GameLiveParticipant, GameLiveParticipant> transform)
    {
        var index = -1;
        for (var i = 0; i < participants.Count; i++)
        {
            if (participants[i].UserId == userId)
            {
                index = i;
                break;
            }
        }

        if (index < 0)
            return null;

        var copy = participants.ToArray();
        copy[index] = transform(copy[index]);
        return copy;
    }

    private static T? Deserialize<T>(JsonElement data) where T : class
    {
        if (data.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
            return null;
        try
        {
            return data.Deserialize<T>(PayloadOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
