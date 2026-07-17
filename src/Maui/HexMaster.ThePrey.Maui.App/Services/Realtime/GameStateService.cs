using System.Text.Json;
using HexMaster.ThePrey.Maui.App.Services.Api;
using HexMaster.ThePrey.Maui.App.Services.Authentication;
using Microsoft.Extensions.Logging;

namespace HexMaster.ThePrey.Maui.App.Services.Realtime;

/// <summary>
/// Default <see cref="IGameStateService"/> — the single in-game store. Subscribes to an
/// <see cref="IGameRealtimeConnection"/>: each real-time envelope is checked for protocol version and
/// sequence continuity, then applied to the in-memory <see cref="GameLiveState"/> composite (and the raw
/// <see cref="GameDetails"/> the lobby reads) as an additive merge onto that one slice. Every (re)connect,
/// a periodic heartbeat, a sequence gap/regression, an unsupported protocol version, and a server
/// <c>resync-requested</c> hint all trigger a full reconcile (game record + rich status + role-specific
/// state) so gaps while the socket was down — and any values the channel never pushes, like the game clock
/// — are healed. Every change is broadcast to subscribers, each isolated so one failure cannot starve the
/// others. The service is UI-agnostic; callers marshal to the UI thread at the subscription boundary.
/// </summary>
public sealed class GameStateService : IGameStateService
{
    /// <summary>Heartbeat cadence for the safety-net reconcile that heals silent drift.</summary>
    private static readonly TimeSpan ReconcileInterval = TimeSpan.FromMinutes(3);

    /// <summary>The highest envelope protocol version this client understands (see <c>docs/api/realtime.md</c>).</summary>
    private const int SupportedProtocolVersion = 1;

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
    private GameDetails? _currentGame;
    private Guid _gameId;
    private long? _lastAppliedSeq;
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

    public GameDetails? CurrentGame
    {
        get { lock (_stateGate) { return _currentGame; } }
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
    private void OnConnectedOrReconnected() => TriggerResync();

    // Fire-and-forget: used on (re)connect, the periodic heartbeat is separate (owns its own timer callback),
    // and every resync trigger below (unsupported version, sequence gap/regression, resync-requested).
    private void TriggerResync() => _ = SafeReconcileAsync();

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
            _currentGame = game;
            // A full snapshot is now authoritative; the next delta's seq becomes the new baseline rather
            // than being checked against whatever we last applied (which may now be stale or unknown).
            _lastAppliedSeq = null;
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

    // Applies one envelope to the current snapshot. Returns the new snapshot when the state changed, or
    // null when it did not (unsupported version, sequence gap, a resync hint, unknown type, malformed
    // payload, missing target, or no baseline snapshot yet) — in every one of those "not applied" cases
    // other than the last, a full resync has been triggered instead so the state still converges.
    private GameLiveState? ApplyEnvelope(GameRealtimeEnvelope envelope)
    {
        if (string.IsNullOrWhiteSpace(envelope.Type))
            return null; // Malformed: no type. Ignore, keep the connection open.

        if (envelope.Version is { } version && version > SupportedProtocolVersion)
        {
            _logger.LogInformation(
                "Received protocol version {Version} (supported: {Supported}); triggering a resync instead of applying it.",
                version, SupportedProtocolVersion);
            TriggerResync();
            return null;
        }

        if (string.Equals(envelope.Type, GameRealtimeEventTypes.ResyncRequested, StringComparison.Ordinal))
        {
            var reason = Deserialize<ResyncRequestedPayload>(envelope.Data)?.Reason ?? "unspecified";
            _logger.LogInformation("Server requested a resync ({Reason}).", reason);
            TriggerResync();
            return null;
        }

        lock (_stateGate)
        {
            if (envelope.Seq is { } seq && !AdmitSequence(seq))
            {
                _logger.LogInformation("Sequence gap/regression detected (seq {Seq}); triggering a resync instead of applying it.", seq);
                TriggerResync();
                return null;
            }

            var current = _current;
            if (current is null)
                return null; // No baseline snapshot yet — nothing to overlay a delta onto.

            var game = _currentGame;
            switch (envelope.Type)
            {
                case GameRealtimeEventTypes.ParticipantJoined:
                case GameRealtimeEventTypes.ParticipantChanged:
                {
                    var payload = Deserialize<ParticipantPayload>(envelope.Data);
                    if (payload is null)
                        return null;

                    var participants = UpsertLiveParticipant(current.Participants, payload);
                    _current = current with
                    {
                        Participants = participants,
                        PreysLeft = CountActivePreys(participants, current.HunterUserId),
                    };
                    if (game is not null)
                        _currentGame = game with { Participants = UpsertGameParticipant(game.Participants, payload) };
                    return _current;
                }

                case GameRealtimeEventTypes.ParticipantRemoved:
                {
                    var payload = Deserialize<ParticipantRemovedPayload>(envelope.Data);
                    if (payload is null)
                        return null;

                    var participants = RemoveById(current.Participants, payload.UserId, p => p.UserId);
                    if (participants is null)
                        return null; // Unknown participant — no-op.

                    _current = current with
                    {
                        Participants = participants,
                        PreysLeft = CountActivePreys(participants, current.HunterUserId),
                    };
                    if (game is not null)
                    {
                        var gameParticipants = RemoveById(game.Participants, payload.UserId, p => p.UserId);
                        if (gameParticipants is not null)
                            _currentGame = game with { Participants = gameParticipants };
                    }
                    return _current;
                }

                case GameRealtimeEventTypes.ConfigurationChanged:
                {
                    var payload = Deserialize<ConfigurationChangedPayload>(envelope.Data);
                    if (payload is null)
                        return null;

                    _current = current with
                    {
                        Status = payload.Status,
                        HunterUserId = payload.HunterUserId,
                        Outcome = payload.Outcome,
                        PreysLeft = CountActivePreys(current.Participants, payload.HunterUserId),
                    };
                    if (game is not null)
                    {
                        _currentGame = game with
                        {
                            GameCode = payload.GameCode,
                            Status = payload.Status,
                            Configuration = payload.Configuration ?? game.Configuration,
                            HunterUserId = payload.HunterUserId,
                            OwnerUserId = payload.OwnerUserId,
                            // Participants, IsOwnerPlayer, and IsReadyToStart are deliberately absent from
                            // this game-level slice — preserved verbatim from the current game record.
                        };
                    }
                    return _current;
                }

                case GameRealtimeEventTypes.LocationsUpdated:
                {
                    var payload = Deserialize<LocationsUpdatedPayload>(envelope.Data);
                    if (payload is null || payload.Locations.Count == 0)
                        return null;

                    var byUserId = payload.Locations.ToDictionary(l => l.UserId);
                    var matched = false;
                    var participants = current.Participants.Select(p =>
                    {
                        if (!byUserId.TryGetValue(p.UserId, out var location))
                            return p;
                        matched = true;
                        return p with { Location = new GpsCoordinate(location.Latitude, location.Longitude), State = location.State };
                    }).ToList();
                    if (!matched)
                        return null;

                    _current = current with
                    {
                        Participants = participants,
                        PreysLeft = CountActivePreys(participants, current.HunterUserId),
                    };
                    if (game is not null)
                    {
                        var gameParticipants = game.Participants.Select(p =>
                            byUserId.TryGetValue(p.UserId, out var location)
                                ? p with { State = location.State, Latitude = location.Latitude, Longitude = location.Longitude }
                                : p).ToList();
                        _currentGame = game with { Participants = gameParticipants };
                    }
                    return _current;
                }

                case GameRealtimeEventTypes.PreyUpdated:
                {
                    var payload = Deserialize<PreyUpdatedPayload>(envelope.Data);
                    if (payload is null)
                        return null;

                    var participants = UpdateById(current.Participants, payload.UserId, p => p.UserId,
                        p => ApplyPreyEvent(p, payload));
                    if (participants is null)
                        return null; // Unknown participant — no-op.

                    _current = current with
                    {
                        Participants = participants,
                        PreysLeft = CountActivePreys(participants, current.HunterUserId),
                    };
                    if (game is not null)
                    {
                        var gameParticipants = UpdateById(game.Participants, payload.UserId, p => p.UserId,
                            p => ApplyPreyEventToGameParticipant(p, payload));
                        if (gameParticipants is not null)
                            _currentGame = game with { Participants = gameParticipants };
                    }
                    return _current;
                }

                case GameRealtimeEventTypes.GameEnded:
                {
                    var payload = Deserialize<GameEndedPayload>(envelope.Data);
                    _current = current with { Status = "Completed", Outcome = payload?.Outcome ?? current.Outcome };
                    return _current;
                }

                default:
                    _logger.LogDebug("Ignoring unhandled game real-time event '{Type}'.", envelope.Type);
                    return null;
            }
        }
    }

    // Must be called while holding _stateGate. Accepts (and records) an in-order seq; rejects a gap or
    // regression without recording it, so the caller can trigger a resync instead of applying the delta.
    private bool AdmitSequence(long seq)
    {
        if (_lastAppliedSeq is { } last && (seq <= last || seq > last + 1))
            return false;
        _lastAppliedSeq = seq;
        return true;
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
            // No in-progress status read (the game is still Lobby/Ready/Started): use the game roster and
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
            Outcome = previous?.Outcome,
        };
    }

    private void Broadcast(GameLiveState state)
    {
        Action<GameStateChanged>[] handlers;
        lock (_subscriberGate) { handlers = _subscribers.ToArray(); }

        GameDetails? game;
        lock (_stateGate) { game = _currentGame; }
        var message = new GameStateChanged(state, game);
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

    // Adds the payload's participant if absent, or replaces it wholesale if present — a full participant
    // payload carries every field this composite tracks, so nothing is lost either way. A penalty flag of
    // false clears any known end time; a flag of true keeps whatever end time we already knew (the exact
    // instant is authoritative only via prey-updated), since this payload does not carry it.
    private static IReadOnlyList<GameLiveParticipant> UpsertLiveParticipant(
        IReadOnlyList<GameLiveParticipant> participants, ParticipantPayload payload)
    {
        var index = IndexOf(participants, payload.UserId, p => p.UserId);
        var previousPenalty = index >= 0 ? participants[index].PenaltyEndsAt : null;
        var updated = new GameLiveParticipant(
            payload.UserId,
            payload.State,
            payload.LastKnownLocation,
            payload.HasActivePenalty ? previousPenalty : null);

        if (index < 0)
            return [.. participants, updated];

        var copy = participants.ToArray();
        copy[index] = updated;
        return copy;
    }

    // Same upsert as above, but for the raw GameDetails.Participants the lobby renders.
    private static IReadOnlyList<GameParticipantDetails> UpsertGameParticipant(
        IReadOnlyList<GameParticipantDetails> participants, ParticipantPayload payload)
    {
        var index = IndexOf(participants, payload.UserId, p => p.UserId);
        var previousPenalty = index >= 0 ? participants[index].PenaltyEndsAt : null;
        var updated = new GameParticipantDetails(
            payload.UserId,
            payload.DisplayName,
            payload.IsReady,
            payload.State,
            payload.LastKnownLocation?.Latitude,
            payload.LastKnownLocation?.Longitude,
            payload.HasActivePenalty ? previousPenalty : null);

        if (index < 0)
            return [.. participants, updated];

        var copy = participants.ToArray();
        copy[index] = updated;
        return copy;
    }

    private static GameLiveParticipant ApplyPreyEvent(GameLiveParticipant participant, PreyUpdatedPayload payload) =>
        payload.Event switch
        {
            GameRealtimeEventTypes.PreyEvents.Tagged => participant with { State = payload.State ?? participant.State },
            GameRealtimeEventTypes.PreyEvents.Penalized => participant with { PenaltyEndsAt = payload.PenaltyEndsAt },
            GameRealtimeEventTypes.PreyEvents.PenaltyCleared => participant with { PenaltyEndsAt = null },
            _ => participant,
        };

    private static GameParticipantDetails ApplyPreyEventToGameParticipant(GameParticipantDetails participant, PreyUpdatedPayload payload) =>
        payload.Event switch
        {
            GameRealtimeEventTypes.PreyEvents.Tagged => participant with { State = payload.State ?? participant.State },
            GameRealtimeEventTypes.PreyEvents.Penalized => participant with { PenaltyEndsAt = payload.PenaltyEndsAt },
            GameRealtimeEventTypes.PreyEvents.PenaltyCleared => participant with { PenaltyEndsAt = null },
            _ => participant,
        };

    private static int IndexOf<T>(IReadOnlyList<T> items, Guid userId, Func<T, Guid> idSelector)
    {
        for (var i = 0; i < items.Count; i++)
        {
            if (idSelector(items[i]) == userId)
                return i;
        }
        return -1;
    }

    // Returns a new list with the matching item transformed, or null when no item matches (a no-op rather
    // than a spurious change/broadcast).
    private static IReadOnlyList<T>? UpdateById<T>(
        IReadOnlyList<T> items, Guid userId, Func<T, Guid> idSelector, Func<T, T> transform)
    {
        var index = IndexOf(items, userId, idSelector);
        if (index < 0)
            return null;

        var copy = items.ToArray();
        copy[index] = transform(copy[index]);
        return copy;
    }

    // Returns a new list with the matching item removed, or null when no item matched (a no-op rather than
    // a spurious change/broadcast).
    private static IReadOnlyList<T>? RemoveById<T>(IReadOnlyList<T> items, Guid userId, Func<T, Guid> idSelector)
    {
        var filtered = items.Where(i => idSelector(i) != userId).ToList();
        return filtered.Count == items.Count ? null : filtered;
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
