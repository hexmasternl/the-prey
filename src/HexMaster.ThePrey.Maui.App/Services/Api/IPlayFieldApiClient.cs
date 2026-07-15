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
}
