namespace HexMaster.ThePrey.Maui.App.Services.Api;

/// <summary>Outcome of joining a game by code (<c>POST /games/{id}/join</c>).</summary>
public enum JoinGameOutcome
{
    Success,
    InvalidCode,
    NotFound,
    Conflict,
    Unauthorized,
    Error
}

/// <summary>
/// Result of <see cref="IGameApiClient.JoinGameAsync"/>. Carries the joined game's id on success and the
/// backend's stable rule <c>code</c> on <see cref="JoinGameOutcome.Conflict"/> / <see cref="JoinGameOutcome.InvalidCode"/>
/// so the page can render a specific message without parsing prose. Mirrors <see cref="ActiveGameResult"/> /
/// <see cref="CreateGameResult"/>. (<see cref="GameSummary"/> is defined once in <c>CreateGameResult.cs</c>.)
/// </summary>
public sealed record JoinGameResult(JoinGameOutcome Outcome, GameSummary? Game, string? Code)
{
    public static JoinGameResult Success(GameSummary game) => new(JoinGameOutcome.Success, game, null);
    public static JoinGameResult InvalidCode(string? code) => new(JoinGameOutcome.InvalidCode, null, code);
    public static readonly JoinGameResult NotFound = new(JoinGameOutcome.NotFound, null, null);
    public static JoinGameResult Conflict(string? code) => new(JoinGameOutcome.Conflict, null, code);
    public static readonly JoinGameResult Unauthorized = new(JoinGameOutcome.Unauthorized, null, null);
    public static readonly JoinGameResult Error = new(JoinGameOutcome.Error, null, null);
}
