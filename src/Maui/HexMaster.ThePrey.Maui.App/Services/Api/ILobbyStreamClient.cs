namespace HexMaster.ThePrey.Maui.App.Services.Api;

/// <summary>
/// Subscribes to a game's live lobby event stream and yields a full <see cref="GameDetails"/> snapshot
/// for every real event (joins, ready changes, hunter designation, settings edits, game start). The
/// implementation owns the transport (SSE), heartbeat filtering, and reconnect-on-drop; callers simply
/// enumerate until they cancel. Kept behind an interface so the lobby view model is unit-testable.
/// </summary>
public interface ILobbyStreamClient
{
    IAsyncEnumerable<GameDetails> Subscribe(Guid gameId, string accessToken, CancellationToken ct);
}
