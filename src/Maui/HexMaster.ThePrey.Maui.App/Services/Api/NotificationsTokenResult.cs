namespace HexMaster.ThePrey.Maui.App.Services.Api;

/// <summary>Outcome of requesting a Web PubSub client access URL (<c>GET /games/{id}/notifications/token</c>).</summary>
public enum NotificationsTokenOutcome
{
    Success,
    Forbidden,
    Unauthorized,
    Error
}

/// <summary>
/// Result of <see cref="IGameApiClient.GetNotificationsTokenAsync"/>. On <see cref="NotificationsTokenOutcome.Success"/>
/// the <see cref="Url"/> is the short-lived, group-scoped WebSocket URL the client connects to.
/// </summary>
public sealed record NotificationsTokenResult(NotificationsTokenOutcome Outcome, string? Url)
{
    public static NotificationsTokenResult Success(string url) => new(NotificationsTokenOutcome.Success, url);
    public static readonly NotificationsTokenResult Forbidden = new(NotificationsTokenOutcome.Forbidden, null);
    public static readonly NotificationsTokenResult Unauthorized = new(NotificationsTokenOutcome.Unauthorized, null);
    public static readonly NotificationsTokenResult Error = new(NotificationsTokenOutcome.Error, null);
}
