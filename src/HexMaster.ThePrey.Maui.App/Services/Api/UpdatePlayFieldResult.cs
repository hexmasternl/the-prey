namespace HexMaster.ThePrey.Maui.App.Services.Api;

/// <summary>Outcome of updating an existing playfield.</summary>
public enum UpdatePlayFieldOutcome
{
    Updated,
    Conflict,
    Validation,
    Unauthorized,
    Forbidden,
    NotFound,
    Error
}

/// <summary>
/// Result of <see cref="IPlayFieldApiClient.UpdatePlayFieldAsync"/>: <c>200</c> → <see cref="Updated"/>
/// (the new summary), <c>409</c> → <see cref="Conflict"/> carrying the server's current state for a
/// reload, <c>400</c> → <see cref="Validation"/>, <c>401</c> → <see cref="Unauthorized"/>,
/// <c>403</c> → <see cref="Forbidden"/>, <c>404</c> → <see cref="NotFound"/>, network/other →
/// <see cref="Error"/>.
/// </summary>
public sealed record UpdatePlayFieldResult(
    UpdatePlayFieldOutcome Outcome,
    PlayFieldSummary? Summary,
    PlayFieldDetails? Current)
{
    public static UpdatePlayFieldResult Updated(PlayFieldSummary summary) =>
        new(UpdatePlayFieldOutcome.Updated, summary, null);

    public static UpdatePlayFieldResult Conflict(PlayFieldDetails current) =>
        new(UpdatePlayFieldOutcome.Conflict, null, current);

    public static readonly UpdatePlayFieldResult Validation = new(UpdatePlayFieldOutcome.Validation, null, null);
    public static readonly UpdatePlayFieldResult Unauthorized = new(UpdatePlayFieldOutcome.Unauthorized, null, null);
    public static readonly UpdatePlayFieldResult Forbidden = new(UpdatePlayFieldOutcome.Forbidden, null, null);
    public static readonly UpdatePlayFieldResult NotFound = new(UpdatePlayFieldOutcome.NotFound, null, null);
    public static readonly UpdatePlayFieldResult Error = new(UpdatePlayFieldOutcome.Error, null, null);
}
