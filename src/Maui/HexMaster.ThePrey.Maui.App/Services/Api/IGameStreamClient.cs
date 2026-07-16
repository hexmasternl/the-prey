namespace HexMaster.ThePrey.Maui.App.Services.Api;

/// <summary>
/// The in-game real-time channel seam: a per-subscription stream of typed <see cref="GameStreamEvent"/>s
/// for one game, delivered over its Azure Web PubSub group. The implementation requests a fresh
/// group-scoped connection URL, opens a native WebSocket, joins the game's group, unwraps each
/// <c>{ type, data }</c> group frame to a typed event, and reconnects with backoff on an unexpected
/// close; enumeration ends when the supplied token is cancelled. Mirrors the lobby stream seam's shape
/// (<see cref="IAsyncEnumerable{T}"/>) so the gameplay view models are transport-agnostic and testable.
/// </summary>
public interface IGameStreamClient
{
    /// <summary>
    /// Subscribes to <paramref name="gameId"/>'s live channel, yielding events until
    /// <paramref name="ct"/> is cancelled. The <paramref name="accessToken"/> seeds the first
    /// connection-URL request; the stream self-refreshes the token on reconnect.
    /// </summary>
    IAsyncEnumerable<GameStreamEvent> Subscribe(Guid gameId, string accessToken, CancellationToken ct);
}
