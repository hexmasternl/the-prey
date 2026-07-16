namespace HexMaster.ThePrey.Maui.App.Services.Api;

/// <summary>Outcome of deleting a playfield via <see cref="IPlayFieldApiClient.DeletePlayFieldAsync"/>.</summary>
public enum DeletePlayFieldOutcome
{
    Success,
    NotFound,
    Forbidden,
    Unauthorized,
    Error
}

/// <summary>
/// Result of <see cref="IPlayFieldApiClient.DeletePlayFieldAsync"/>: <c>204</c> → <see cref="Success"/>,
/// <c>404</c> → <see cref="NotFound"/>, <c>403</c> → <see cref="Forbidden"/>, <c>401</c> →
/// <see cref="Unauthorized"/>, network/timeout/unexpected → <see cref="Error"/>. Mirrors the
/// result-union shape of the other endpoints on this client. Both <c>Success</c> and <c>NotFound</c>
/// mean the desired end state — the playfield is gone — is reached.
/// </summary>
public sealed record DeletePlayFieldResult(DeletePlayFieldOutcome Outcome)
{
    public static readonly DeletePlayFieldResult Success = new(DeletePlayFieldOutcome.Success);
    public static readonly DeletePlayFieldResult NotFound = new(DeletePlayFieldOutcome.NotFound);
    public static readonly DeletePlayFieldResult Forbidden = new(DeletePlayFieldOutcome.Forbidden);
    public static readonly DeletePlayFieldResult Unauthorized = new(DeletePlayFieldOutcome.Unauthorized);
    public static readonly DeletePlayFieldResult Error = new(DeletePlayFieldOutcome.Error);
}
