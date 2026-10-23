namespace HexMaster.ThePrey.Games.Api.Endpoints;

/// <summary>
/// Internal, service-to-service endpoints (invoked over Dapr, not exposed through the gateway).
/// </summary>
public static class InternalGameEndpoints
{
    public static IEndpointRouteBuilder MapInternalGameEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/internal/games")
            .WithTags("InternalGames");

        // Membership check used by the Notifications module before issuing a Web PubSub access token.
        // Always returns 200 with an explicit { isMember } flag so callers can distinguish a real
        // "not a member" answer from a 404 (this endpoint not deployed / route missing).
        group.MapGet("/{gameId:guid}/members/{userId:guid}", IsMember)
            .WithName("IsGameMember")
            .Produces<GameMembershipResponse>(StatusCodes.Status200OK)
            .AllowAnonymous();

        return app;
    }

    private static async Task<IResult> IsMember(
        Guid gameId,
        Guid userId,
        IGameRepository games,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger("InternalGameEndpoints");

        var game = await games.GetByIdAsync(gameId, ct);
        var isMember = game is not null && game.IsVisibleTo(userId);

        logger.LogInformation(
            "Membership check for user {UserId} on game {GameId}: gameFound={GameFound}, isMember={IsMember}.",
            userId, gameId, game is not null, isMember);

        return Results.Ok(new GameMembershipResponse(isMember));
    }
}

/// <summary>Result of an internal game-membership check.</summary>
public sealed record GameMembershipResponse(bool IsMember);
