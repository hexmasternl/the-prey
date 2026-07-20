namespace HexMaster.ThePrey.Maui.App.Services.Location;

/// <summary>
/// Default <see cref="IGameLocationTracker"/> — a thin façade over
/// <see cref="GameLocationTrackerCoordinator"/>, which owns all tracking logic. Kept separate so callers
/// depend only on the small public contract while the coordinator stays directly unit-testable.
/// Registered as a singleton so one tracker serves the whole app session.
/// </summary>
public sealed class GameLocationTracker : IGameLocationTracker
{
    private readonly GameLocationTrackerCoordinator _coordinator;

    public GameLocationTracker(GameLocationTrackerCoordinator coordinator) => _coordinator = coordinator;

    public Task StartAsync(Guid gameId, TimeSpan? remaining = null, CancellationToken ct = default) =>
        _coordinator.StartAsync(gameId, remaining, ct);

    public Task StopAsync() => _coordinator.StopAsync();
}
