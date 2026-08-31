namespace HexMaster.ThePrey.Maui.App.Services.Api;

/// <summary>
/// Client-side projection of the backend <c>FullGameStateDto</c> returned by
/// <c>GET /games/{id}/gamestate</c> — the complete server-side state of one game for the calling user,
/// in a single response. This is the app's only snapshot read: it replaces composing
/// <c>GET /games/{id}</c> + <c>/status</c> + <c>/state</c>, and unlike <c>/status</c> it is served in
/// <em>every</em> game status, so the lobby, gameplay, and outcome screens all use it.
/// <para>
/// <see cref="Status"/> and <see cref="RoleState"/> are <c>null</c> outside <c>InProgress</c>;
/// <see cref="Game"/> and the playfield are always populated. Deserializes case-insensitively from the
/// backend's camelCase JSON.
/// </para>
/// </summary>
/// <param name="ServerTime">
/// The server's clock when the snapshot was built — the reference for any locally ticked countdown,
/// so the UI does not drift with the device clock.
/// </param>
public sealed record FullGameState(
    GameDetails Game,
    string PlayfieldName,
    IReadOnlyList<GpsCoordinate> PlayfieldCoordinates,
    GameStatusDetails? Status,
    GameStateSnapshot? RoleState,
    DateTimeOffset ServerTime);

/// <summary>Outcome of the composite state read (<c>GET /games/{id}/gamestate</c>).</summary>
public enum FullGameStateOutcome
{
    Success,

    /// <summary>The caller is not a member of this game (403).</summary>
    Forbidden,
    NotFound,
    Unauthorized,
    Error
}

/// <summary>Result of <see cref="IGameApiClient.GetFullGameStateAsync"/>.</summary>
public sealed record FullGameStateResult(FullGameStateOutcome Outcome, FullGameState? State)
{
    public static FullGameStateResult Success(FullGameState state) => new(FullGameStateOutcome.Success, state);
    public static readonly FullGameStateResult Forbidden = new(FullGameStateOutcome.Forbidden, null);
    public static readonly FullGameStateResult NotFound = new(FullGameStateOutcome.NotFound, null);
    public static readonly FullGameStateResult Unauthorized = new(FullGameStateOutcome.Unauthorized, null);
    public static readonly FullGameStateResult Error = new(FullGameStateOutcome.Error, null);
}
