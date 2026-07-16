using HexMaster.ThePrey.Maui.App.Services.Realtime;

namespace HexMaster.ThePrey.Maui.App.Tests;

/// <summary>
/// Hand-driven <see cref="IGameStateService"/> for the gameplay/HUD view-model tests. <see cref="StartAsync"/>
/// seeds (and returns) <see cref="SeedState"/>; <see cref="Push"/> delivers a fresh snapshot to every
/// subscriber synchronously, so tests can drive live updates without a real socket or timers.
/// </summary>
internal sealed class FakeGameStateService : IGameStateService
{
    private readonly List<Action<GameStateChanged>> _subscribers = new();

    public GameLiveState? CurrentState { get; private set; }

    /// <summary>The snapshot <see cref="StartAsync"/> seeds and returns; <c>null</c> models "no active game".</summary>
    public GameLiveState? SeedState { get; set; }

    public bool StartAsyncCalled { get; private set; }
    public bool Stopped { get; private set; }
    public int SubscriberCount => _subscribers.Count;

    public Task<GameLiveState?> StartAsync(CancellationToken ct = default)
    {
        StartAsyncCalled = true;
        if (SeedState is not null)
            CurrentState = SeedState;
        return Task.FromResult(CurrentState);
    }

    public void Start(Guid gameId) { }

    public Task StopAsync()
    {
        Stopped = true;
        return Task.CompletedTask;
    }

    public void Subscribe(Action<GameStateChanged> handler) => _subscribers.Add(handler);
    public void Unsubscribe(Action<GameStateChanged> handler) => _subscribers.Remove(handler);

    /// <summary>Delivers a new snapshot to every current subscriber (and updates <see cref="CurrentState"/>).</summary>
    public void Push(GameLiveState state)
    {
        CurrentState = state;
        foreach (var handler in _subscribers.ToArray())
            handler(new GameStateChanged(state));
    }
}
