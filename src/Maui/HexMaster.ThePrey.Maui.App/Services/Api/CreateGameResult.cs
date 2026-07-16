namespace HexMaster.ThePrey.Maui.App.Services.Api;

/// <summary>
/// Minimal projection of the created game returned by <c>POST /games</c> — only the id the config page
/// needs to move on to the game route. Deserialized from the backend's camelCase <c>GameDto</c>.
/// </summary>
public sealed record GameSummary(Guid Id);

/// <summary>Outcome of creating a game (<c>POST /games</c>).</summary>
public enum CreateGameOutcome
{
    Success,
    Validation,
    Unauthorized,
    Error
}

/// <summary>Result of <see cref="IGameApiClient.CreateGameAsync"/>. Carries the created game's id on success.</summary>
public sealed record CreateGameResult(CreateGameOutcome Outcome, GameSummary? Game)
{
    public static CreateGameResult Success(GameSummary game) => new(CreateGameOutcome.Success, game);
    public static readonly CreateGameResult Validation = new(CreateGameOutcome.Validation, null);
    public static readonly CreateGameResult Unauthorized = new(CreateGameOutcome.Unauthorized, null);
    public static readonly CreateGameResult Error = new(CreateGameOutcome.Error, null);
}
