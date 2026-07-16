namespace HexMaster.ThePrey.Maui.App.Services.Location;

/// <summary>How the backend responded to a single location report.</summary>
public enum LocationReportOutcome
{
    /// <summary>200 — the fix was recorded; <see cref="LocationReportResult.Response"/> carries the next cadence.</summary>
    Accepted,

    /// <summary>404/422 — the game is no longer InProgress; the tracker should stop.</summary>
    GameOver,

    /// <summary>401 — the access token was rejected; refresh it and retry on the next tick.</summary>
    Unauthorized,

    /// <summary>Network error, timeout, 5xx, or an unreadable body — retry on the next tick.</summary>
    Transient
}

/// <summary>Result of a single <see cref="ILocationReportClient.ReportAsync"/> call.</summary>
public sealed record LocationReportResult(LocationReportOutcome Outcome, RecordLocationResponse? Response = null)
{
    public static LocationReportResult Accepted(RecordLocationResponse response) =>
        new(LocationReportOutcome.Accepted, response);

    public static readonly LocationReportResult GameOver = new(LocationReportOutcome.GameOver);
    public static readonly LocationReportResult Unauthorized = new(LocationReportOutcome.Unauthorized);
    public static readonly LocationReportResult Transient = new(LocationReportOutcome.Transient);
}

/// <summary>
/// Reports a single device position to <c>POST /games/{id}/locations</c> with a bearer token and maps
/// the backend status codes to a <see cref="LocationReportResult"/>. Never throws — transport failures
/// map to <see cref="LocationReportOutcome.Transient"/>.
/// </summary>
public interface ILocationReportClient
{
    Task<LocationReportResult> ReportAsync(
        Guid gameId, RecordLocationRequest request, string accessToken, CancellationToken ct = default);
}
