namespace HexMaster.ThePrey.Games.Abstractions.DataTransferObjects;

/// <summary>
/// The complete, server-authoritative state of one game as seen by one caller — everything a client
/// needs to render the lobby, the gameplay map, or the outcome screen, in a single response.
/// Returned by <c>GET /games/{id}/gamestate</c>, which replaces the three-call composition of
/// <c>GET /games/{id}</c> + <c>GET /games/{id}/status</c> + <c>GET /games/{id}/state</c>.
/// <para>
/// Unlike <c>GET /games/{id}/status</c>, this is available for a game in <em>any</em> status: the two
/// in-progress-only slices (<see cref="Status"/> and <see cref="RoleState"/>) are simply
/// <c>null</c> before the game starts and after it completes, while <see cref="Game"/> and the
/// playfield are always populated.
/// </para>
/// </summary>
/// <param name="Game">
/// The full game record from this caller's perspective: configuration, every participant with their
/// last known location / state / penalty flag, the hunter, timing, outcome, and the per-caller
/// <c>IsOwnerPlayer</c> / <c>IsReadyToStart</c> flags. Always populated.
/// </param>
/// <param name="PlayfieldName">The name of the playfield the game is played in. Always populated.</param>
/// <param name="PlayfieldCoordinates">
/// The playfield's boundary polygon, for drawing the play area. Always populated (empty when the
/// playfield can no longer be resolved).
/// </param>
/// <param name="Status">
/// The rich in-progress status — remaining game time, this caller's ping cadence, endgame flag,
/// surviving prey count, hunter-may-move moment. <c>null</c> unless the game is in progress.
/// </param>
/// <param name="RoleState">
/// The role-specific view: a hunter gets every prey's last broadcast location, a prey gets its
/// distance to the hunter. <c>null</c> unless the game is in progress and the caller plays in it
/// (a lobby-visible non-participant gets the game but no role state).
/// </param>
/// <param name="ServerTime">
/// The server's clock at the moment this snapshot was built. Clients tick their countdowns locally
/// between snapshots, so this lets them correct for request latency and device clock drift rather
/// than assuming their own clock matches the server's.
/// </param>
public sealed record FullGameStateDto(
    GameDto Game,
    string PlayfieldName,
    IReadOnlyList<GpsCoordinateDto> PlayfieldCoordinates,
    GameStatusDto? Status,
    GameStateDto? RoleState,
    DateTimeOffset ServerTime);
