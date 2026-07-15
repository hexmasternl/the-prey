namespace HexMaster.ThePrey.Maui.App.Services.Api;

/// <summary>Outcome of an owner settings update (<c>PUT /games/{id}/config</c>).</summary>
public enum UpdateGameSettingsOutcome
{
    Success,
    Validation,
    Forbidden,
    Unauthorized,
    Error
}

/// <summary>Result of <see cref="IGameApiClient.UpdateGameSettingsAsync"/>. Carries the refreshed game snapshot on success.</summary>
public sealed record UpdateGameSettingsResult(UpdateGameSettingsOutcome Outcome, GameDetails? Game)
{
    public static UpdateGameSettingsResult Success(GameDetails game) => new(UpdateGameSettingsOutcome.Success, game);
    public static readonly UpdateGameSettingsResult Validation = new(UpdateGameSettingsOutcome.Validation, null);
    public static readonly UpdateGameSettingsResult Forbidden = new(UpdateGameSettingsOutcome.Forbidden, null);
    public static readonly UpdateGameSettingsResult Unauthorized = new(UpdateGameSettingsOutcome.Unauthorized, null);
    public static readonly UpdateGameSettingsResult Error = new(UpdateGameSettingsOutcome.Error, null);
}
