using HexMaster.ThePrey.Maui.App.Services.Api;

namespace HexMaster.ThePrey.Maui.App.Services.Realtime;

/// <summary>
/// Notification broadcast by <see cref="IGameStateService"/> after the authoritative local game state
/// changes. Carries the current <see cref="State"/> so subscribed components can re-render without
/// re-querying the service.
/// </summary>
public sealed record GameStateChanged(GameDetails State);
