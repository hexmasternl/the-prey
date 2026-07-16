namespace HexMaster.ThePrey.Maui.App.Services.Api;

/// <summary>
/// Outcome of tagging a prey (<c>POST /games/{id}/participants/{participantId}/tag</c>).
/// <see cref="Conflict"/> maps the backend's <c>409</c> (prey no longer taggable — moved out of range,
/// already tagged, or game not in progress).
/// </summary>
public enum TagPlayerOutcome
{
    Success,
    Forbidden,
    NotFound,
    Conflict,
    Unauthorized,
    Error
}

/// <summary>Result of <see cref="IGameApiClient.TagPlayerAsync"/>.</summary>
public sealed record TagPlayerResult(TagPlayerOutcome Outcome)
{
    public static readonly TagPlayerResult Success = new(TagPlayerOutcome.Success);
    public static readonly TagPlayerResult Forbidden = new(TagPlayerOutcome.Forbidden);
    public static readonly TagPlayerResult NotFound = new(TagPlayerOutcome.NotFound);
    public static readonly TagPlayerResult Conflict = new(TagPlayerOutcome.Conflict);
    public static readonly TagPlayerResult Unauthorized = new(TagPlayerOutcome.Unauthorized);
    public static readonly TagPlayerResult Error = new(TagPlayerOutcome.Error);
}
