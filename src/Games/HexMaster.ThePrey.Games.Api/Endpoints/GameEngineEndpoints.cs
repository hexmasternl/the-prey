using HexMaster.ThePrey.Core;
using HexMaster.ThePrey.Games.Abstractions.DataTransferObjects;
using HexMaster.ThePrey.Games.Features.UpdateLocationBroadcast;
using HexMaster.ThePrey.Games.Notifications;
using Microsoft.AspNetCore.Mvc;

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
        [FromBody] LocationUpdateRequest request,
        HttpContext httpContext,
        ICommandHandler<UpdateLocationBroadcastCommand, UpdateLocationBroadcastResult> handler,
        IConfiguration configuration,
        CancellationToken ct)
    {
        var expectedKey = configuration["GameEngine:EngineKey"];
        if (!string.IsNullOrEmpty(expectedKey))
        {
            var providedKey = httpContext.Request.Headers["X-Engine-Key"].FirstOrDefault();
            if (providedKey != expectedKey)
                return Results.Unauthorized();
        }

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
            var json = System.Text.Json.JsonSerializer.Serialize(evt);
            await httpContext.Response.WriteAsync($"event: location-update\ndata: {json}\n\n", ct);
            await httpContext.Response.Body.FlushAsync(ct);
        }
    }
}
