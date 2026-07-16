namespace HexMaster.ThePrey.Maui.App.Services.Api;

/// <summary>
/// Outcome of reading the rich in-progress status (<c>GET /games/{id}/status</c>) for the gameplay map.
/// The endpoint serves only in-progress games, so <see cref="Forbidden"/> (not a participant) and
/// <see cref="Conflict"/> (not in progress yet / already finished) are expected while a game is still
/// <c>Ready</c> — the gameplay view model treats both as "not live yet" rather than errors.
/// </summary>
public enum GetGameStatusOutcome
{
    Success,
    Forbidden,
    Conflict,
    NotFound,
    Unauthorized,
    Error
}

/// <summary>
/// Result of <see cref="IGameApiClient.GetGameStatusDetailsAsync"/>. Carries the rich
/// <see cref="GameStatusDetails"/> snapshot on success.
/// </summary>
public sealed record GetGameStatusResult(GetGameStatusOutcome Outcome, GameStatusDetails? Details)
{
    public static GetGameStatusResult Success(GameStatusDetails details) => new(GetGameStatusOutcome.Success, details);
    public static readonly GetGameStatusResult Forbidden = new(GetGameStatusOutcome.Forbidden, null);
    public static readonly GetGameStatusResult Conflict = new(GetGameStatusOutcome.Conflict, null);
    public static readonly GetGameStatusResult NotFound = new(GetGameStatusOutcome.NotFound, null);
    public static readonly GetGameStatusResult Unauthorized = new(GetGameStatusOutcome.Unauthorized, null);
    public static readonly GetGameStatusResult Error = new(GetGameStatusOutcome.Error, null);
}
