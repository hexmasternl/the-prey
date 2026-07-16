using System.Text.Json;
using HexMaster.ThePrey.Maui.App.Services.Api;
using HexMaster.ThePrey.Maui.App.Services.Authentication;
using Microsoft.Extensions.Logging;

namespace HexMaster.ThePrey.Maui.App.Services.Realtime;

/// <summary>
/// Default <see cref="IGameStateService"/>. Subscribes to an <see cref="IGameRealtimeConnection"/>:
/// each real-time envelope is applied to the in-memory <see cref="GameDetails"/> snapshot, and every
/// (re)connect triggers a full-snapshot reconcile via <see cref="IGameApiClient.GetGameAsync"/> so gaps
/// while the socket was down are healed. Any change is broadcast to subscribers, with each subscriber
/// isolated so one failure cannot starve the others.
/// </summary>
public sealed class GameStateService : IGameStateService
{
    private static readonly JsonSerializerOptions PayloadOptions = new(JsonSerializerDefaults.Web);

    private readonly IGameRealtimeConnection _connection;
    private readonly IGameApiClient _gameApi;
    private readonly IAccessTokenProvider _accessTokenProvider;
    private readonly ILogger<GameStateService> _logger;

    private readonly object _stateGate = new();
    private readonly object _subscriberGate = new();
    private readonly List<Action<GameStateChanged>> _subscribers = new();

    private GameDetails? _current;
    private Guid _gameId;

    public GameStateService(
        IGameRealtimeConnection connection,
        IGameApiClient gameApi,
        IAccessTokenProvider accessTokenProvider,
        ILogger<GameStateService> logger)
    {
        _connection = connection;
        _gameApi = gameApi;
        _accessTokenProvider = accessTokenProvider;
        _logger = logger;

        _connection.EnvelopeReceived += OnEnvelopeReceived;
        _connection.Connected += OnConnectedOrReconnected;
        _connection.Reconnected += OnConnectedOrReconnected;
        _connection.Unavailable += OnUnavailable;
    }

    public GameDetails? CurrentState
    {
        get { lock (_stateGate) { return _current; } }
    }

    public void Start(Guid gameId)
    {
        _gameId = gameId;
        _connection.Start(gameId);
    }

    public Task StopAsync() => _connection.StopAsync();

    public void Subscribe(Action<GameStateChanged> handler)
    {
        lock (_subscriberGate) { _subscribers.Add(handler); }
    }

    public void Unsubscribe(Action<GameStateChanged> handler)
    {
        lock (_subscriberGate) { _subscribers.Remove(handler); }
    }

    // On (re)connect, pull the authoritative snapshot so any events missed while the socket was down are
    // reconciled. Fire-and-forget: the connection's event is synchronous and must not block its loop.
    private void OnConnectedOrReconnected() => _ = ReconcileAsync();

    private async Task ReconcileAsync()
    {
        try
        {
            var accessToken = await _accessTokenProvider.GetAccessTokenAsync();
            if (string.IsNullOrWhiteSpace(accessToken))
            {
                _logger.LogWarning("Cannot reconcile game state: no access token available.");
                return;
            }

            var result = await _gameApi.GetGameAsync(_gameId, accessToken!);
            if (result.Outcome == GetGameOutcome.Success && result.Game is not null)
            {
                SetState(result.Game);
            }
            else
            {
                _logger.LogWarning("Game-state reconcile for {GameId} returned {Outcome}.", _gameId, result.Outcome);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Game-state reconcile for {GameId} failed.", _gameId);
        }
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
    private GameDetails? ApplyEnvelope(GameRealtimeEnvelope envelope)
    {
        if (string.IsNullOrWhiteSpace(envelope.Type))
            return null;

        lock (_stateGate)
        {
            if (GameRealtimeEventTypes.FullSnapshotEvents.Contains(envelope.Type))
            {
                var snapshot = Deserialize<GameDetails>(envelope.Data);
                if (snapshot is null)
                    return null;
                _current = snapshot;
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
                        Latitude = payload.Latitude,
                        Longitude = payload.Longitude,
                        State = string.IsNullOrEmpty(payload.ParticipantState) ? p.State : payload.ParticipantState,
                    });
                    if (participants is null)
                        return null;
                    _current = current with { Participants = participants };
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
                    _current = current with { Participants = participants };
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

    private void SetState(GameDetails state)
    {
        lock (_stateGate) { _current = state; }
        Broadcast(state);
    }

    private void Broadcast(GameDetails state)
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

    // Returns a new participant list with the matching participant transformed, or null when no
    // participant matches (so the caller can treat it as a no-op instead of a spurious change).
    private static IReadOnlyList<GameParticipantDetails>? UpdateParticipant(
        IReadOnlyList<GameParticipantDetails> participants,
        Guid userId,
        Func<GameParticipantDetails, GameParticipantDetails> transform)
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
