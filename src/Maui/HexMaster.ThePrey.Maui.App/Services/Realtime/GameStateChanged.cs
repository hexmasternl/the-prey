using HexMaster.ThePrey.Maui.App.Services.Api;

namespace HexMaster.ThePrey.Maui.App.Services.Realtime;

/// <summary>
/// Notification broadcast by <see cref="IGameStateService"/> after the authoritative local
/// <see cref="GameLiveState"/> changes. Carries the current <see cref="State"/> so subscribed UI
/// components can re-render without re-querying the service. <see cref="Game"/> is the raw game record the
/// state was built from (present on every snapshot/reconcile and on full-snapshot lobby events; the lobby
/// reads it for the fields <see cref="GameLiveState"/> does not carry — pass code, configuration,
/// participant names/ready, owner). It is <c>null</c> only before the first snapshot has been seen.
/// </summary>
public sealed record GameStateChanged(GameLiveState State, GameDetails? Game = null);
