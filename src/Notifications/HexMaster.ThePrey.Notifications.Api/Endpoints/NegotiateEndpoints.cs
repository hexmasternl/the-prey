using System.Diagnostics;
using System.Security.Claims;
using Azure.Messaging.WebPubSub;
using HexMaster.ThePrey.Notifications;
using HexMaster.ThePrey.Notifications.Abstractions.DataTransferObjects;
using HexMaster.ThePrey.Notifications.Observability;
using HexMaster.ThePrey.Users.Integration;

namespace HexMaster.ThePrey.Notifications.Api.Endpoints;

/// <summary>
/// The negotiate endpoint a client calls (authenticated) before connecting to Web PubSub. It verifies
/// the caller is a member of the game, then returns a short-lived, group-scoped access URL that lets
/// the client open a WebSocket and automatically join the game's group.
/// </summary>
public static class NegotiateEndpoints
{
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromHours(1);
    private static readonly ActivitySource ActivitySource = new(NotificationsObservabilityConstants.ActivitySourceName);

    public static IEndpointRouteBuilder MapNegotiateEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/notifications/games/{gameId:guid}/negotiate", Negotiate)
            .WithName("NegotiateGameNotifications")
            .Produces<NegotiateResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .RequireAuthorization();

        return app;
    }

    private static async Task<IResult> Negotiate(
        Guid gameId,
        ClaimsPrincipal principal,
        WebPubSubServiceClient client,
        IUserResolver userResolver,
        IGameMembershipProvider membership,
        INotificationsMetrics metrics,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger("Notifications.Negotiate");

        using var activity = ActivitySource.StartActivity("Notifications.Negotiate");
        activity?.SetTag("game.id", gameId);

        var subjectId = principal.FindFirstValue("sub");
        if (subjectId is null)
        {
            metrics.RecordNegotiate("unauthorized");
            activity?.SetTag("negotiate.outcome", "unauthorized");
            logger.LogWarning("Negotiate for game {GameId} rejected: no 'sub' claim.", gameId);
            return Results.Unauthorized();
        }

        var user = await userResolver.ResolveUser(subjectId, ct);
        if (user is null)
        {
            metrics.RecordNegotiate("unauthorized");
            activity?.SetTag("negotiate.outcome", "unauthorized");
            logger.LogWarning("Negotiate for game {GameId} rejected: subject did not resolve to a user.", gameId);
            return Results.Unauthorized();
        }

        activity?.SetTag("user.id", user.UserId);

        if (!await membership.IsMemberAsync(gameId, user.UserId, ct))
        {
            metrics.RecordNegotiate("forbidden");
            activity?.SetTag("negotiate.outcome", "forbidden");
            logger.LogWarning("Negotiate for game {GameId} forbidden: user {UserId} is not a member.", gameId, user.UserId);
            return Results.Forbid();
        }

        try
        {
            var groups = new[] { gameId.ToString() };
            var uri = client.GetClientAccessUri(
                expiresAfter: TokenLifetime,
                userId: subjectId,
                roles: null,
                groups: groups);

            metrics.RecordNegotiate("granted");
            activity?.SetTag("negotiate.outcome", "granted");
            logger.LogInformation("Issued Web PubSub access for user {UserId} on game {GameId}.", user.UserId, gameId);
            return Results.Ok(new NegotiateResponse(uri.AbsoluteUri));
        }
        catch (Exception ex)
        {
            metrics.RecordNegotiate("error");
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            logger.LogError(ex, "Failed to mint Web PubSub access for user {UserId} on game {GameId}.", user.UserId, gameId);
            throw;
        }
    }
}
