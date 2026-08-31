namespace HexMaster.ThePrey.Maui.App.Services.Api;

/// <summary>
/// Outcome of leaving a game (<c>POST /games/{id}/leave</c>). Leaving is a lobby-only action:
/// <see cref="Conflict"/> is the server refusing because the game is already under way (or finished),
/// which is an expected answer rather than a fault — a started game cannot be left.
/// </summary>
public enum LeaveGameOutcome
{
    Success,

    /// <summary>The game has already started or completed and can no longer be left (409).</summary>
    Conflict,

    /// <summary>The game does not exist, or the caller is not in its lobby (404).</summary>
    NotFound,
    Unauthorized,
    Error
}

/// <summary>Result of <see cref="IGameApiClient.LeaveGameAsync"/>.</summary>
public sealed record LeaveGameResult(LeaveGameOutcome Outcome)
{
    public static readonly LeaveGameResult Success = new(LeaveGameOutcome.Success);
    public static readonly LeaveGameResult Conflict = new(LeaveGameOutcome.Conflict);
    public static readonly LeaveGameResult NotFound = new(LeaveGameOutcome.NotFound);
    public static readonly LeaveGameResult Unauthorized = new(LeaveGameOutcome.Unauthorized);
    public static readonly LeaveGameResult Error = new(LeaveGameOutcome.Error);
}
