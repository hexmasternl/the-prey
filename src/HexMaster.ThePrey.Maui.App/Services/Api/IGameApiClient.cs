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
}
