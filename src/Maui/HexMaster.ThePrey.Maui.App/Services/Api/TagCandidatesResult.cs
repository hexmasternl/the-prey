namespace HexMaster.ThePrey.Maui.App.Services.Api;

/// <summary>Outcome of listing the preys a hunter may tag (<c>GET /games/{id}/tag-candidates</c>).</summary>
public enum TagCandidatesOutcome
{
    Success,
    Forbidden,
    NotFound,
    Unauthorized,
    Error
}

/// <summary>
/// Result of <see cref="IGameApiClient.GetTagCandidatesAsync"/>. On success carries the candidate preys
/// (possibly empty) and the tag <see cref="RangeMeters"/>. <see cref="TagCandidatesOutcome.Forbidden"/>
/// maps the backend's <c>403</c> for a non-hunter caller.
/// </summary>
public sealed record TagCandidatesResult(
    TagCandidatesOutcome Outcome,
    IReadOnlyList<TagCandidate> Candidates,
    double RangeMeters)
{
    private static readonly IReadOnlyList<TagCandidate> Empty = Array.Empty<TagCandidate>();

    public static TagCandidatesResult Success(IReadOnlyList<TagCandidate> candidates, double rangeMeters) =>
        new(TagCandidatesOutcome.Success, candidates, rangeMeters);

    public static readonly TagCandidatesResult Forbidden = new(TagCandidatesOutcome.Forbidden, Empty, 0);
    public static readonly TagCandidatesResult NotFound = new(TagCandidatesOutcome.NotFound, Empty, 0);
    public static readonly TagCandidatesResult Unauthorized = new(TagCandidatesOutcome.Unauthorized, Empty, 0);
    public static readonly TagCandidatesResult Error = new(TagCandidatesOutcome.Error, Empty, 0);
}
