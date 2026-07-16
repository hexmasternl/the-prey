namespace HexMaster.ThePrey.Maui.App.Services.Api;

/// <summary>Outcome of starting the operation (<c>POST /games/{id}/start</c>).</summary>
public enum StartGameOutcome
{
    Success,
    Validation,
    Forbidden,
    NotFound,
    Unauthorized,
    Error
}

/// <summary>Result of <see cref="IGameApiClient.StartGameAsync"/>. Carries the started game snapshot on success.</summary>
public sealed record StartGameResult(StartGameOutcome Outcome, GameDetails? Game)
{
    public static StartGameResult Success(GameDetails game) => new(StartGameOutcome.Success, game);
    public static readonly StartGameResult Validation = new(StartGameOutcome.Validation, null);
    public static readonly StartGameResult Forbidden = new(StartGameOutcome.Forbidden, null);
    public static readonly StartGameResult NotFound = new(StartGameOutcome.NotFound, null);
    public static readonly StartGameResult Unauthorized = new(StartGameOutcome.Unauthorized, null);
    public static readonly StartGameResult Error = new(StartGameOutcome.Error, null);
}
