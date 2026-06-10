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
        group.MapGet("/{gameId:guid}/members/{userId:guid}", IsMember)
            .WithName("IsGameMember")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .AllowAnonymous();

        return app;
    }

    private static async Task<IResult> IsMember(
        Guid gameId,
        Guid userId,
        IGameRepository games,
        CancellationToken ct)
    {
        var game = await games.GetByIdAsync(gameId, ct);
        return game is not null && game.IsVisibleTo(userId)
            ? Results.NoContent()
            : Results.NotFound();
    }
}
