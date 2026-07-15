namespace HexMaster.ThePrey.Maui.App.Services.Api;

/// <summary>Outcome of a non-owner readying up (<c>POST /games/{id}/lobby/ready</c>).</summary>
public enum SetReadyOutcome
{
    Success,
    Forbidden,
    NotFound,
    Unauthorized,
    Error
}

/// <summary>Result of <see cref="IGameApiClient.SetReadyAsync"/>. Carries the refreshed game snapshot on success.</summary>
public sealed record SetReadyResult(SetReadyOutcome Outcome, GameDetails? Game)
{
    public static SetReadyResult Success(GameDetails game) => new(SetReadyOutcome.Success, game);
    public static readonly SetReadyResult Forbidden = new(SetReadyOutcome.Forbidden, null);
    public static readonly SetReadyResult NotFound = new(SetReadyOutcome.NotFound, null);
    public static readonly SetReadyResult Unauthorized = new(SetReadyOutcome.Unauthorized, null);
    public static readonly SetReadyResult Error = new(SetReadyOutcome.Error, null);
}
