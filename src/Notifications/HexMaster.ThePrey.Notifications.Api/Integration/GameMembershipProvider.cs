using System.Diagnostics;
using System.Net;
using Dapr.Client;
using HexMaster.ThePrey.Notifications;
using HexMaster.ThePrey.Notifications.Observability;
using Microsoft.Extensions.Logging;
using ThePrey.Aspire.ServiceDefaults;

namespace HexMaster.ThePrey.Notifications.Api.Integration;

/// <summary>
/// Resolves game membership by invoking the Games module over Dapr service invocation. The Games
/// endpoint always answers 200 with an explicit <c>isMember</c> flag; a 404 therefore means the
/// endpoint itself is missing (e.g. the Games service has not been deployed with it), which is
/// surfaced as a distinct error rather than silently treated as "not a member".
/// </summary>
public sealed class GameMembershipProvider : IGameMembershipProvider
{
    private static readonly string GamesAppId = AspireConstants.Resources.GamesApi;
    private static readonly ActivitySource ActivitySource = new(NotificationsObservabilityConstants.ActivitySourceName);

    private readonly DaprClient _dapr;
    private readonly INotificationsMetrics _metrics;
    private readonly ILogger<GameMembershipProvider> _logger;

    public GameMembershipProvider(DaprClient dapr, INotificationsMetrics metrics, ILogger<GameMembershipProvider> logger)
    {
        _dapr = dapr;
        _metrics = metrics;
        _logger = logger;
    }

    public async Task<bool> IsMemberAsync(Guid gameId, Guid userId, CancellationToken ct)
    {
        using var activity = ActivitySource.StartActivity("Notifications.MembershipCheck");
        activity?.SetTag("game.id", gameId);
        activity?.SetTag("user.id", userId);

        var start = Stopwatch.GetTimestamp();
        try
        {
            var request = _dapr.CreateInvokeMethodRequest(
                HttpMethod.Get,
                GamesAppId,
                $"internal/games/{gameId}/members/{userId}");

            // See PlayfieldInfoProvider: this overload is the only mockable one.
#pragma warning disable CS0618
            var response = await _dapr.InvokeMethodAsync<GameMembershipResult>(request, ct);
#pragma warning restore CS0618

            var isMember = response?.IsMember ?? false;
            RecordResult(activity, start, isMember);
            return isMember;
        }
        catch (InvocationException ex) when (ex.Response?.StatusCode == HttpStatusCode.NotFound)
        {
            // The membership route is not present on the Games service — almost always a deployment
            // gap (Games not redeployed with the internal endpoint). Fail closed, but make it loud.
            activity?.SetTag("membership.endpoint_missing", true);
            activity?.SetStatus(ActivityStatusCode.Error, "Games membership endpoint returned 404.");
            _metrics.RecordMembershipCheck(false, Stopwatch.GetElapsedTime(start).TotalMilliseconds);
            _logger.LogError(
                "Games membership endpoint returned 404 for game {GameId}. Ensure the Games service is deployed with the internal members endpoint.",
                gameId);
            return false;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            _logger.LogError(ex, "Membership check failed for user {UserId} on game {GameId}.", userId, gameId);
            throw;
        }
    }

    private void RecordResult(Activity? activity, long start, bool isMember)
    {
        var elapsedMs = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        activity?.SetTag("membership.is_member", isMember);
        _metrics.RecordMembershipCheck(isMember, elapsedMs);
        _logger.LogInformation(
            "Membership check resolved {Result} in {ElapsedMs:F0}ms.",
            isMember ? "member" : "non-member", elapsedMs);
    }

    private sealed record GameMembershipResult(bool IsMember);
}
