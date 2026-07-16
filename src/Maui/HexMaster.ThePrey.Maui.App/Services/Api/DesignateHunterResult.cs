namespace HexMaster.ThePrey.Maui.App.Services.Api;

/// <summary>Outcome of designating the hunter (<c>POST /games/{id}/hunter</c>).</summary>
public enum DesignateHunterOutcome
{
    Success,
    Forbidden,
    NotFound,
    Unauthorized,
    Error
}

/// <summary>Result of <see cref="IGameApiClient.DesignateHunterAsync"/>. Carries the refreshed game snapshot on success.</summary>
public sealed record DesignateHunterResult(DesignateHunterOutcome Outcome, GameDetails? Game)
{
    public static DesignateHunterResult Success(GameDetails game) => new(DesignateHunterOutcome.Success, game);
    public static readonly DesignateHunterResult Forbidden = new(DesignateHunterOutcome.Forbidden, null);
    public static readonly DesignateHunterResult NotFound = new(DesignateHunterOutcome.NotFound, null);
    public static readonly DesignateHunterResult Unauthorized = new(DesignateHunterOutcome.Unauthorized, null);
    public static readonly DesignateHunterResult Error = new(DesignateHunterOutcome.Error, null);
}
