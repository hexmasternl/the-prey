namespace HexMaster.ThePrey.Maui.App.Services.Api;

/// <summary>Outcome of reading an in-progress game's role-specific state (<c>GET /games/{id}/state</c>).</summary>
public enum GameStateOutcome
{
    Success,
    NotFound,
    Unauthorized,
    Error
}

/// <summary>Result of <see cref="IGameApiClient.GetGameStateAsync"/>. Carries the state snapshot on success.</summary>
public sealed record GameStateResult(GameStateOutcome Outcome, GameStateSnapshot? State)
{
    public static GameStateResult Success(GameStateSnapshot state) => new(GameStateOutcome.Success, state);
    public static readonly GameStateResult NotFound = new(GameStateOutcome.NotFound, null);
    public static readonly GameStateResult Unauthorized = new(GameStateOutcome.Unauthorized, null);
    public static readonly GameStateResult Error = new(GameStateOutcome.Error, null);
}
