namespace HexMaster.ThePrey.Maui.App.Services.Api;

/// <summary>
/// The full client-side projection of a playfield — everything the edit flow needs that the list
/// <see cref="PlayFieldSummary"/> omits: the ordered polygon <see cref="Points"/> and the
/// <see cref="LastUpdatedOn"/> concurrency stamp that a non-stale <c>PUT</c> must echo back. Fetched by
/// <see cref="IPlayFieldApiClient.GetPlayFieldAsync"/> and carried on a <c>409</c> conflict.
/// </summary>
public sealed record PlayFieldDetails(
    Guid Id,
    string Name,
    bool IsPublic,
    IReadOnlyList<GpsCoordinate> Points,
    DateTimeOffset LastUpdatedOn);
