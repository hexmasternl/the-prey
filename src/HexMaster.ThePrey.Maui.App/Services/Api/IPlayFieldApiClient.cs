namespace HexMaster.ThePrey.Maui.App.Services.Api;

/// <summary>Outcome of retrieving the current user's playfields.</summary>
public enum MyPlayFieldsOutcome
{
    Success,
    Unauthorized,
    Error
}

/// <summary>Result of <see cref="IPlayFieldApiClient.GetMyPlayFieldsAsync"/>.</summary>
public sealed record MyPlayFieldsResult(MyPlayFieldsOutcome Outcome, IReadOnlyList<PlayFieldSummary> Items)
{
    public static MyPlayFieldsResult Success(IReadOnlyList<PlayFieldSummary> items) =>
        new(MyPlayFieldsOutcome.Success, items);

    public static readonly MyPlayFieldsResult Unauthorized = new(MyPlayFieldsOutcome.Unauthorized, []);
    public static readonly MyPlayFieldsResult Error = new(MyPlayFieldsOutcome.Error, []);
}

/// <summary>Outcome of searching public playfields.</summary>
public enum PublicPlayFieldsOutcome
{
    Success,
    ValidationTooShort,
    Unauthorized,
    Error
}

/// <summary>Result of <see cref="IPlayFieldApiClient.SearchPublicPlayFieldsAsync"/>.</summary>
public sealed record PublicPlayFieldsResult(PublicPlayFieldsOutcome Outcome, IReadOnlyList<PlayFieldSummary> Items)
{
    public static PublicPlayFieldsResult Success(IReadOnlyList<PlayFieldSummary> items) =>
        new(PublicPlayFieldsOutcome.Success, items);

    public static readonly PublicPlayFieldsResult ValidationTooShort = new(PublicPlayFieldsOutcome.ValidationTooShort, []);
    public static readonly PublicPlayFieldsResult Unauthorized = new(PublicPlayFieldsOutcome.Unauthorized, []);
    public static readonly PublicPlayFieldsResult Error = new(PublicPlayFieldsOutcome.Error, []);
}

/// <summary>Calls the backend authorized playfield endpoints on behalf of the signed-in user.</summary>
public interface IPlayFieldApiClient
{
    /// <summary>Reads <c>GET /playfields</c> — the caller's own playfields — with the supplied bearer token.</summary>
    Task<MyPlayFieldsResult> GetMyPlayFieldsAsync(string accessToken, CancellationToken ct = default);

    /// <summary>Searches <c>GET /playfields/public?q=&lt;query&gt;</c> with the supplied bearer token.</summary>
    Task<PublicPlayFieldsResult> SearchPublicPlayFieldsAsync(string query, string accessToken, CancellationToken ct = default);

    /// <summary>
    /// Creates a playfield via <c>POST /playfields</c> with the supplied name, visibility, polygon points,
    /// and bearer token. Maps <c>201</c> → success (the created summary), <c>400</c> → validation,
    /// <c>401</c> → unauthenticated, network/timeout/unexpected → error.
    /// </summary>
    Task<CreatePlayFieldResult> CreatePlayFieldAsync(
        string name,
        bool isPublic,
        IReadOnlyList<GpsCoordinate> points,
        string accessToken,
        CancellationToken ct = default);

    /// <summary>
    /// Deletes a playfield via <c>DELETE /playfields/{id}</c> with the supplied bearer token. Maps
    /// <c>204</c> → success, <c>404</c> → not-found, <c>403</c> → forbidden, <c>401</c> → unauthenticated,
    /// network/timeout/unexpected → error. Never throws for these outcomes.
    /// </summary>
    Task<DeletePlayFieldResult> DeletePlayFieldAsync(Guid id, string accessToken, CancellationToken ct = default);

    /// <summary>
    /// Loads a single playfield (with its polygon and concurrency stamp) via <c>GET /playfields/{id}</c>.
    /// Maps <c>200</c> → success, <c>404</c> → not-found, <c>401</c> → unauthenticated,
    /// network/timeout/unexpected → error. Never throws for these outcomes.
    /// </summary>
    Task<GetPlayFieldResult> GetPlayFieldAsync(Guid id, string accessToken, CancellationToken ct = default);

    /// <summary>
    /// Updates a playfield via <c>PUT /playfields/{id}</c> with the supplied fields and the loaded
    /// <paramref name="lastUpdatedOn"/> concurrency stamp. Maps <c>200</c> → updated, <c>409</c> → conflict
    /// (carrying the server's current state), <c>400</c> → validation, <c>401</c> → unauthenticated,
    /// <c>403</c> → forbidden, <c>404</c> → not-found, network/timeout/unexpected → error. Never throws.
    /// </summary>
    Task<UpdatePlayFieldResult> UpdatePlayFieldAsync(
        Guid id,
        string name,
        bool isPublic,
        IReadOnlyList<GpsCoordinate> points,
        DateTimeOffset lastUpdatedOn,
        string accessToken,
        CancellationToken ct = default);
}
