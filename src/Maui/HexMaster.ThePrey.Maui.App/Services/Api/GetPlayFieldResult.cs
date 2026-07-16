namespace HexMaster.ThePrey.Maui.App.Services.Api;

/// <summary>Outcome of loading a single playfield by id.</summary>
public enum GetPlayFieldOutcome
{
    Success,
    NotFound,
    Unauthorized,
    Error
}

/// <summary>
/// Result of <see cref="IPlayFieldApiClient.GetPlayFieldAsync"/>: <c>200</c> → <see cref="Success"/>
/// carrying the full <see cref="PlayFieldDetails"/>, <c>404</c> → <see cref="NotFound"/>,
/// <c>401</c> → <see cref="Unauthorized"/>, network/timeout/unexpected → <see cref="Error"/>.
/// </summary>
public sealed record GetPlayFieldResult(GetPlayFieldOutcome Outcome, PlayFieldDetails? PlayField)
{
    public static GetPlayFieldResult Success(PlayFieldDetails playField) =>
        new(GetPlayFieldOutcome.Success, playField);

    public static readonly GetPlayFieldResult NotFound = new(GetPlayFieldOutcome.NotFound, null);
    public static readonly GetPlayFieldResult Unauthorized = new(GetPlayFieldOutcome.Unauthorized, null);
    public static readonly GetPlayFieldResult Error = new(GetPlayFieldOutcome.Error, null);
}
