using System.Text.Json;
using HexMaster.ThePrey.Core;
using HexMaster.ThePrey.Games.Abstractions.DataTransferObjects;
using HexMaster.ThePrey.Games.Features.UpdateLocationBroadcast;
using HexMaster.ThePrey.Games.Notifications;
using HexMaster.ThePrey.Games.Security;

namespace HexMaster.ThePrey.Games.Api.Endpoints;

public static class GameEngineEndpoints
{
    public static IEndpointRouteBuilder MapGameEngineEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/game-engine")
            .WithTags("GameEngine");

        group.MapPost("/{gameId:guid}/location-update", LocationUpdate)
            .WithName("GameEngineLocationUpdate")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status422UnprocessableEntity)
            .AllowAnonymous();

        group.MapGet("/{gameId:guid}/stream", StreamEngineEvents)
            .WithName("StreamEngineEvents")
            .Produces(StatusCodes.Status200OK, contentType: "text/event-stream")
            .Produces(StatusCodes.Status404NotFound)
            .RequireAuthorization();

        return app;
    }

    private static async Task<IResult> LocationUpdate(
        Guid gameId,
        HttpContext httpContext,
        ICommandHandler<UpdateLocationBroadcastCommand, UpdateLocationBroadcastResult> handler,
        IConfiguration configuration,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger(nameof(GameEngineEndpoints));
        var secret = configuration["GameEngine:EngineKey"];
        if (string.IsNullOrEmpty(secret))
        {
            logger.LogWarning(
                "GameEngine:EngineKey is not configured. Rejecting location-update request for game {GameId}.",
                gameId);
            return Results.Unauthorized();
        }

        // Buffer the raw body so we can verify the signature over its exact bytes.
        httpContext.Request.EnableBuffering();
        using var ms = new MemoryStream();
        await httpContext.Request.Body.CopyToAsync(ms, ct);
        var bodyBytes = ms.ToArray();

        var timestamp = httpContext.Request.Headers[EngineRequestSigner.TimestampHeaderName].FirstOrDefault();
        var signature = httpContext.Request.Headers[EngineRequestSigner.SignatureHeaderName].FirstOrDefault();

        if (!EngineRequestSigner.Verify(secret, timestamp, signature, bodyBytes, DateTimeOffset.UtcNow))
        {
            logger.LogWarning(
                "HMAC verification failed for location-update on game {GameId}. Timestamp header: {Timestamp}",
                gameId,
                timestamp);
            return Results.Unauthorized();
        }

        LocationUpdateRequest? request;
        try
        {
            request = JsonSerializer.Deserialize<LocationUpdateRequest>(
                bodyBytes,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException)
        {
            return Results.UnprocessableEntity();
        }

        if (request is null)
            return Results.UnprocessableEntity();

        var locations = request.Locations
            .Select(l => new ParticipantLocationUpdate(l.UserId, l.Latitude, l.Longitude))
            .ToList();

        try
        {
            await handler.Handle(new UpdateLocationBroadcastCommand(gameId, locations), ct);
            return Results.NoContent();
        }
        catch (KeyNotFoundException)
        {
            return Results.NotFound();
        }
        catch (InvalidOperationException)
        {
            return Results.UnprocessableEntity();
        }
    }

    private static async Task StreamEngineEvents(
        Guid gameId,
        IGameRepository gameRepository,
        IGameEngineEventBus engineEventBus,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var game = await gameRepository.GetByIdAsync(gameId, ct);
        if (game is null)
        {
            httpContext.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        httpContext.Response.ContentType = "text/event-stream";
        httpContext.Response.Headers.CacheControl = "no-cache";
        httpContext.Response.Headers.Append("X-Accel-Buffering", "no");

        await foreach (var evt in engineEventBus.Subscribe(gameId).WithCancellation(ct))
        {
            var json = JsonSerializer.Serialize(evt);
            await httpContext.Response.WriteAsync($"event: location-update\ndata: {json}\n\n", ct);
            await httpContext.Response.Body.FlushAsync(ct);
        }
    }
}
