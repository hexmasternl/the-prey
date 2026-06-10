using System.Diagnostics;
using System.Net;
using Dapr.Client;
using HexMaster.ThePrey.Notifications;
using HexMaster.ThePrey.Notifications.Observability;
using Microsoft.Extensions.Logging;
using ThePrey.Aspire.ServiceDefaults;

namespace HexMaster.ThePrey.Notifications.Api.Integration;

/// <summary>
/// Resolves game membership by invoking the Games module over Dapr service invocation. A 2xx response
/// means the user is the owner or a participant; a 404 means they are not a member.
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

            // See PlayfieldInfoProvider: this HttpRequestMessage overload is the only mockable one.
#pragma warning disable CS0618
            await _dapr.InvokeMethodAsync(request, ct);
#pragma warning restore CS0618

            RecordResult(activity, start, isMember: true);
            return true;
        }
        catch (InvocationException ex) when (ex.Response?.StatusCode == HttpStatusCode.NotFound)
        {
            RecordResult(activity, start, isMember: false);
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
}
