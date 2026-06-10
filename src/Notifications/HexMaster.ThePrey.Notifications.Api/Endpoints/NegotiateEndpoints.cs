using System.Security.Claims;
using Azure.Messaging.WebPubSub;
using HexMaster.ThePrey.Notifications;
using HexMaster.ThePrey.Notifications.Abstractions.DataTransferObjects;
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
        CancellationToken ct)
    {
        var subjectId = principal.FindFirstValue("sub");
        if (subjectId is null) return Results.Unauthorized();

        var user = await userResolver.ResolveUser(subjectId, ct);
        if (user is null) return Results.Unauthorized();

        if (!await membership.IsMemberAsync(gameId, user.UserId, ct))
            return Results.Forbid();

        var groups = new[] { gameId.ToString() };
        var uri = client.GetClientAccessUri(
            expiresAfter: TokenLifetime,
            userId: subjectId,
            roles: null,
            groups: groups);

        return Results.Ok(new NegotiateResponse(uri.AbsoluteUri));
    }
}
