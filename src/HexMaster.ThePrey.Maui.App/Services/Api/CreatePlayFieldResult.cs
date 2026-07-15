namespace HexMaster.ThePrey.Maui.App.Services.Api;

/// <summary>Outcome of creating a playfield via <see cref="IPlayFieldApiClient.CreatePlayFieldAsync"/>.</summary>
public enum CreatePlayFieldOutcome
{
    Success,
    Validation,
    Unauthorized,
    Error
}

/// <summary>
/// Result of <see cref="IPlayFieldApiClient.CreatePlayFieldAsync"/>: <c>201</c> → <see cref="Success"/>
/// carrying the created <see cref="PlayFieldSummary"/>, <c>400</c> → <see cref="Validation"/>,
/// <c>401</c> → <see cref="Unauthorized"/>, network/timeout/unexpected → <see cref="Error"/>. Mirrors
/// the result-union shape of the read endpoints on this client.
/// </summary>
public sealed record CreatePlayFieldResult(CreatePlayFieldOutcome Outcome, PlayFieldSummary? PlayField)
{
    public static CreatePlayFieldResult Success(PlayFieldSummary playField) =>
        new(CreatePlayFieldOutcome.Success, playField);

    public static readonly CreatePlayFieldResult Validation = new(CreatePlayFieldOutcome.Validation, null);
    public static readonly CreatePlayFieldResult Unauthorized = new(CreatePlayFieldOutcome.Unauthorized, null);
    public static readonly CreatePlayFieldResult Error = new(CreatePlayFieldOutcome.Error, null);
}
