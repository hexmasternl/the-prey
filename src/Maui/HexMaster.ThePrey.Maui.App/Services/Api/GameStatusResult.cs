namespace HexMaster.ThePrey.Maui.App.Services.Api;

/// <summary>Outcome of reading an in-progress game's status (<c>GET /games/{id}/status</c>).</summary>
public enum GameStatusOutcome
{
    Success,
    NotFound,
    Forbidden,
    Completed,
    Unauthorized,
    Error
}

/// <summary>
/// Result of <see cref="IGameApiClient.GetGameStatusAsync"/>. Carries the status snapshot on success.
/// <see cref="GameStatusOutcome.Completed"/> maps the backend's <c>409</c> for a finished game, which
/// tells the HUD to stop ticking/polling and hand off to the host.
/// </summary>
public sealed record GameStatusResult(GameStatusOutcome Outcome, GameStatusSnapshot? Status)
{
    public static GameStatusResult Success(GameStatusSnapshot status) => new(GameStatusOutcome.Success, status);
    public static readonly GameStatusResult NotFound = new(GameStatusOutcome.NotFound, null);
    public static readonly GameStatusResult Forbidden = new(GameStatusOutcome.Forbidden, null);
    public static readonly GameStatusResult Completed = new(GameStatusOutcome.Completed, null);
    public static readonly GameStatusResult Unauthorized = new(GameStatusOutcome.Unauthorized, null);
    public static readonly GameStatusResult Error = new(GameStatusOutcome.Error, null);
}
