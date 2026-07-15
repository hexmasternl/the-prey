namespace HexMaster.ThePrey.Maui.App.Services.Api;

/// <summary>Outcome of the active-game backend query.</summary>
public enum ActiveGameOutcome
{
    HasActiveGame,
    NoActiveGame,
    Unauthorized,
    Error
}

/// <summary>Result of <see cref="IGameApiClient.GetActiveGameAsync"/>.</summary>
public sealed record ActiveGameResult(ActiveGameOutcome Outcome, GameStatus? Game)
{
    public static ActiveGameResult Active(GameStatus game) => new(ActiveGameOutcome.HasActiveGame, game);
    public static readonly ActiveGameResult None = new(ActiveGameOutcome.NoActiveGame, null);
    public static readonly ActiveGameResult Unauthorized = new(ActiveGameOutcome.Unauthorized, null);
    public static readonly ActiveGameResult Error = new(ActiveGameOutcome.Error, null);
}

/// <summary>Calls the backend game endpoints on behalf of the signed-in user.</summary>
public interface IGameApiClient
{
    /// <summary>Queries <c>GET /games/active</c> with the supplied bearer access token.</summary>
    Task<ActiveGameResult> GetActiveGameAsync(string accessToken, CancellationToken ct = default);

    /// <summary>Reads the full state of a game via <c>GET /games/{id}</c>.</summary>
    Task<GetGameResult> GetGameAsync(Guid gameId, string accessToken, CancellationToken ct = default);

    /// <summary>
    /// Persists the owner's game settings via <c>PUT /games/{id}/config</c>. The two ping intervals in
    /// <paramref name="settings"/> are given in minutes and sent as seconds (× 60); the three durations
    /// are sent as their minute values.
    /// </summary>
    Task<UpdateGameSettingsResult> UpdateGameSettingsAsync(
        Guid gameId, GameSettingsParameters settings, string accessToken, CancellationToken ct = default);

    /// <summary>Designates the hunter via <c>POST /games/{id}/hunter</c> (owner-only in the lobby).</summary>
    Task<DesignateHunterResult> DesignateHunterAsync(
        Guid gameId, Guid newHunterUserId, string accessToken, CancellationToken ct = default);

    /// <summary>Marks the calling (non-owner) player ready via <c>POST /games/{id}/lobby/ready</c>.</summary>
    Task<SetReadyResult> SetReadyAsync(Guid gameId, string accessToken, CancellationToken ct = default);

    /// <summary>Starts the operation via <c>POST /games/{id}/start</c>, designating <paramref name="hunterUserId"/>.</summary>
    Task<StartGameResult> StartGameAsync(
        Guid gameId, Guid hunterUserId, string accessToken, CancellationToken ct = default);
}
