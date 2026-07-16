namespace HexMaster.ThePrey.Maui.App.Services.Api;

/// <summary>Outcome of reading a single game's full state (<c>GET /games/{id}</c>).</summary>
public enum GetGameOutcome
{
    Success,
    NotFound,
    Unauthorized,
    Error
}

/// <summary>Result of <see cref="IGameApiClient.GetGameAsync"/>.</summary>
public sealed record GetGameResult(GetGameOutcome Outcome, GameDetails? Game)
{
    public static GetGameResult Success(GameDetails game) => new(GetGameOutcome.Success, game);
    public static readonly GetGameResult NotFound = new(GetGameOutcome.NotFound, null);
    public static readonly GetGameResult Unauthorized = new(GetGameOutcome.Unauthorized, null);
    public static readonly GetGameResult Error = new(GetGameOutcome.Error, null);
}
