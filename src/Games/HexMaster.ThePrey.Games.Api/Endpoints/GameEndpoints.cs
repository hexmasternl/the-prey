using System.Security.Claims;
using HexMaster.ThePrey.Core;
using HexMaster.ThePrey.Games.Abstractions.DataTransferObjects;
using HexMaster.ThePrey.Games.DomainModels;
using HexMaster.ThePrey.Games.Features.CreateGame;
using HexMaster.ThePrey.Games.Features.EndGame;
using HexMaster.ThePrey.Games.Features.GetActiveGame;
using HexMaster.ThePrey.Games.Features.GetGame;
using HexMaster.ThePrey.Games.Features.GetGameState;
using HexMaster.ThePrey.Games.Features.GetGameStatus;
using HexMaster.ThePrey.Games.Features.JoinGame;
using HexMaster.ThePrey.Games.Features.LeaveGame;
using HexMaster.ThePrey.Games.Features.ListGames;
using HexMaster.ThePrey.Games.Features.RecordPlayerLocation;
using HexMaster.ThePrey.Games.Features.RemoveLobbyPlayer;
using HexMaster.ThePrey.Games.Features.SetHunter;
using HexMaster.ThePrey.Games.Features.SetReady;
using HexMaster.ThePrey.Games.Features.StartGame;
using HexMaster.ThePrey.Games.Features.TagPlayer;
using HexMaster.ThePrey.Games.Features.UpdateGameSettings;
using HexMaster.ThePrey.Games.Notifications;
using HexMaster.ThePrey.Users.Integration;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace HexMaster.ThePrey.Games.Api.Endpoints;

public static class GameEndpoints
{
    /// <summary>
    /// camelCase options for the SSE payloads so the JSON keys match the web/Minimal-API response
    /// contract the Angular client consumes (e.g. <c>payload</c>, <c>isOwnerPlayer</c>). The default
    /// serializer would emit PascalCase, which the client cannot read.
    /// </summary>
    private static readonly System.Text.Json.JsonSerializerOptions SseJsonOptions =
        new(System.Text.Json.JsonSerializerDefaults.Web);

    public static IEndpointRouteBuilder MapGameEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/games")
            .WithTags("Games")
            .RequireAuthorization();

        group.MapPost("/", CreateGame)
            .WithName("CreateGame")
            .Produces<GameDto>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status401Unauthorized);

        group.MapPost("/{id:guid}/lobby", JoinGame)
            .WithName("JoinGame")
            .Produces<GameDto>()
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/join", JoinGame)
            .WithName("JoinGameByCode")
            .Produces<GameDto>()
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/start", StartGame)
            .WithName("StartGame")
            .Produces<GameDto>()
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/hunter", SetHunter)
            .WithName("SetHunter")
            .Produces<GameDto>()
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/locations", RecordLocation)
            .WithName("RecordPlayerLocation")
            .Produces<RecordLocationResponse>()
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/{id:guid}", GetGame)
            .WithName("GetGame")
            .Produces<GameDto>()
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/{id:guid}/state", GetGameState)
            .WithName("GetGameState")
            .Produces<GameStateDto>()
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/", ListGames)
            .WithName("ListGames")
            .Produces<IReadOnlyList<GameSummaryDto>>();

        group.MapGet("/active", GetActiveGame)
            .WithName("GetActiveGame")
            .Produces<GameStatusDto>()
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status401Unauthorized);

        group.MapGet("/{id:guid}/status", GetGameStatus)
            .WithName("GetGameStatus")
            .Produces<GameStatusDto>()
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status409Conflict)
            .Produces(StatusCodes.Status401Unauthorized);

        group.MapDelete("/{id:guid}/lobby/{userId:guid}", RemoveLobbyPlayer)
            .WithName("RemoveLobbyPlayer")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPut("/{id:guid}/config", UpdateGameConfig)
            .WithName("UpdateGameConfig")
            .Produces<GameDto>()
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/lobby/ready", SetReady)
            .WithName("SetReady")
            .Produces<GameDto>()
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/participants/{participantId:guid}/tag", TagPlayer)
            .WithName("TagPlayer")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict)
            .Produces(StatusCodes.Status401Unauthorized);

        group.MapPost("/{id:guid}/end", EndGame)
            .WithName("EndGame")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict)
            .Produces(StatusCodes.Status401Unauthorized);

        group.MapPost("/{id:guid}/leave", LeaveGame)
            .WithName("LeaveGame")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict)
            .Produces(StatusCodes.Status401Unauthorized);

        group.MapGet("/{id:guid}/lobby/stream", StreamLobbyEvents)
            .WithName("StreamLobbyEvents")
            .Produces(StatusCodes.Status200OK, contentType: "text/event-stream")
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status401Unauthorized);

        group.MapGet("/{id:guid}/stream", StreamGameEvents)
            .WithName("StreamGameEvents")
            .Produces(StatusCodes.Status200OK, contentType: "text/event-stream")
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status401Unauthorized);

        return app;
    }

    private static async Task<IResult> CreateGame(
        [FromBody] CreateGameRequest request,
        ClaimsPrincipal principal,
        IUserResolver userResolver,
        ICommandHandler<CreateGameCommand, CreateGameResult> handler,
        CancellationToken ct)
    {
        var subjectId = principal.FindFirstValue("sub");
        if (subjectId is null) return Results.Unauthorized();
        var user = await userResolver.ResolveUser(subjectId, ct);
        if (user is null) return Results.Unauthorized();

        var command = new CreateGameCommand(
            user.UserId,
            request.PlayfieldId,
            request.DisplayName,
            request.ProfilePictureUrl,
            request.GameDuration,
            request.HunterDelayTime,
            request.FinalStageDuration,
            request.DefaultLocationInterval,
            request.FinalLocationInterval,
            request.EnablePreyBoundaryPenalties,
            request.EnableHunterBoundaryPenalty);

        try
        {
            var result = await handler.Handle(command, ct);
            return Results.Created($"/games/{result.Game.Id}", result.Game);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ValidationProblem(ex);
        }
    }

    private static async Task<IResult> JoinGame(
        Guid id,
        [FromBody] JoinGameRequest request,
        ClaimsPrincipal principal,
        IUserResolver userResolver,
        ICommandHandler<JoinGameCommand, JoinGameResult?> handler,
        CancellationToken ct)
    {
        var subjectId = principal.FindFirstValue("sub");
        if (subjectId is null) return Results.Unauthorized();
        var user = await userResolver.ResolveUser(subjectId, ct);
        if (user is null) return Results.Unauthorized();

        try
        {
            var result = await handler.Handle(new JoinGameCommand(id, user.UserId, request.JoinCode, request.DisplayName, request.ProfilePictureUrl), ct);
            return result is null
                ? GameRuleProblem("game_not_found", "The game does not exist.", StatusCodes.Status404NotFound)
                : Results.Ok(result.Game);
        }
        catch (GameRuleException ex)
        {
            // Surface a stable, machine-readable code the client maps to a localized message.
            // An invalid join code is a bad request; the rest are conflicts with the game's state.
            var status = ex is InvalidJoinCodeException
                ? StatusCodes.Status400BadRequest
                : StatusCodes.Status409Conflict;
            return GameRuleProblem(ex.Code, ex.Message, status);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ValidationProblem(ex);
        }
    }

    private static async Task<IResult> StartGame(
        Guid id,
        [FromBody] StartGameRequest request,
        ClaimsPrincipal principal,
        IUserResolver userResolver,
        ICommandHandler<StartGameCommand, StartGameResult?> handler,
        CancellationToken ct)
    {
        var subjectId = principal.FindFirstValue("sub");
        if (subjectId is null) return Results.Unauthorized();
        var user = await userResolver.ResolveUser(subjectId, ct);
        if (user is null) return Results.Unauthorized();

        try
        {
            var result = await handler.Handle(new StartGameCommand(id, user.UserId, request.HunterUserId), ct);
            return result is null ? Results.NotFound() : Results.Ok(result.Game);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ValidationProblem(ex);
        }
    }

    private static async Task<IResult> SetHunter(
        Guid id,
        [FromBody] SetHunterRequest request,
        ClaimsPrincipal principal,
        IUserResolver userResolver,
        ICommandHandler<SetHunterCommand, SetHunterResult?> handler,
        CancellationToken ct)
    {
        var subjectId = principal.FindFirstValue("sub");
        if (subjectId is null) return Results.Unauthorized();
        var user = await userResolver.ResolveUser(subjectId, ct);
        if (user is null) return Results.Unauthorized();

        try
        {
            var result = await handler.Handle(new SetHunterCommand(id, user.UserId, request.NewHunterUserId), ct);
            return result is null ? Results.NotFound() : Results.Ok(result.Game);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ValidationProblem(ex);
        }
    }

    private static async Task<IResult> RecordLocation(
        Guid id,
        [FromBody] RecordLocationRequest request,
        ClaimsPrincipal principal,
        IUserResolver userResolver,
        ICommandHandler<RecordPlayerLocationCommand, RecordPlayerLocationResult?> handler,
        CancellationToken ct)
    {
        var subjectId = principal.FindFirstValue("sub");
        if (subjectId is null) return Results.Unauthorized();
        var user = await userResolver.ResolveUser(subjectId, ct);
        if (user is null) return Results.Unauthorized();

        try
        {
            var result = await handler.Handle(
                new RecordPlayerLocationCommand(id, user.UserId, request.Latitude, request.Longitude, request.RecordedAt, request.Accuracy), ct);
            return result is null ? Results.NotFound() : Results.Ok(result.Response);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ValidationProblem(ex);
        }
    }

    private static async Task<IResult> GetGame(
        Guid id,
        ClaimsPrincipal principal,
        IUserResolver userResolver,
        IQueryHandler<GetGameQuery, GameDto?> handler,
        CancellationToken ct)
    {
        var subjectId = principal.FindFirstValue("sub");
        if (subjectId is null) return Results.Unauthorized();
        var user = await userResolver.ResolveUser(subjectId, ct);
        if (user is null) return Results.Unauthorized();

        var game = await handler.Handle(new GetGameQuery(id, user.UserId), ct);
        return game is not null ? Results.Ok(game) : Results.NotFound();
    }

    private static async Task<IResult> GetGameState(
        Guid id,
        ClaimsPrincipal principal,
        IUserResolver userResolver,
        IQueryHandler<GetGameStateQuery, GameStateDto?> handler,
        CancellationToken ct)
    {
        var subjectId = principal.FindFirstValue("sub");
        if (subjectId is null) return Results.Unauthorized();
        var user = await userResolver.ResolveUser(subjectId, ct);
        if (user is null) return Results.Unauthorized();

        var state = await handler.Handle(new GetGameStateQuery(id, user.UserId), ct);
        return state is not null ? Results.Ok(state) : Results.NotFound();
    }

    private static async Task<IResult> ListGames(
        ClaimsPrincipal principal,
        IUserResolver userResolver,
        IQueryHandler<ListGamesQuery, IReadOnlyList<GameSummaryDto>> handler,
        CancellationToken ct)
    {
        var subjectId = principal.FindFirstValue("sub");
        if (subjectId is null) return Results.Unauthorized();
        var user = await userResolver.ResolveUser(subjectId, ct);
        if (user is null) return Results.Unauthorized();

        var games = await handler.Handle(new ListGamesQuery(user.UserId), ct);
        return Results.Ok(games);
    }

    private static async Task<IResult> GetActiveGame(
        ClaimsPrincipal principal,
        IUserResolver userResolver,
        IQueryHandler<GetActiveGameQuery, GameStatusDto?> handler,
        CancellationToken ct)
    {
        var subjectId = principal.FindFirstValue("sub");
        if (subjectId is null) return Results.Unauthorized();
        var user = await userResolver.ResolveUser(subjectId, ct);
        if (user is null) return Results.Unauthorized();

        var active = await handler.Handle(new GetActiveGameQuery(user.UserId), ct);
        return active is not null ? Results.Ok(active) : Results.NotFound();
    }

    private static async Task<IResult> GetGameStatus(
        Guid id,
        ClaimsPrincipal principal,
        IUserResolver userResolver,
        IQueryHandler<GetGameStatusQuery, GameStatusDto?> handler,
        CancellationToken ct)
    {
        var subjectId = principal.FindFirstValue("sub");
        if (subjectId is null) return Results.Unauthorized();
        var user = await userResolver.ResolveUser(subjectId, ct);
        if (user is null) return Results.Unauthorized();

        try
        {
            var status = await handler.Handle(new GetGameStatusQuery(id, user.UserId), ct);
            return status is not null ? Results.Ok(status) : Results.NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return Results.Forbid();
        }
        catch (InvalidOperationException)
        {
            return Results.Conflict();
        }
    }

    private static async Task<IResult> RemoveLobbyPlayer(
        Guid id,
        Guid userId,
        ClaimsPrincipal principal,
        IUserResolver userResolver,
        ICommandHandler<RemoveLobbyPlayerCommand, RemoveLobbyPlayerResult?> handler,
        CancellationToken ct)
    {
        var subjectId = principal.FindFirstValue("sub");
        if (subjectId is null) return Results.Unauthorized();
        var user = await userResolver.ResolveUser(subjectId, ct);
        if (user is null) return Results.Unauthorized();

        try
        {
            var result = await handler.Handle(new RemoveLobbyPlayerCommand(id, user.UserId, userId), ct);
            if (result is null) return Results.NotFound();
            return Results.NoContent();
        }
        catch (InvalidOperationException)
        {
            return Results.Forbid();
        }
        catch (Exception ex) when (ex is ArgumentException)
        {
            return ValidationProblem(ex);
        }
    }

    private static async Task<IResult> UpdateGameConfig(
        Guid id,
        [FromBody] UpdateGameSettingsRequest request,
        ClaimsPrincipal principal,
        IUserResolver userResolver,
        ICommandHandler<UpdateGameSettingsCommand, UpdateGameSettingsResult?> handler,
        CancellationToken ct)
    {
        var subjectId = principal.FindFirstValue("sub");
        if (subjectId is null) return Results.Unauthorized();
        var user = await userResolver.ResolveUser(subjectId, ct);
        if (user is null) return Results.Unauthorized();

        try
        {
            var command = new UpdateGameSettingsCommand(
                id,
                user.UserId,
                request.GameDuration,
                request.HunterDelayTime,
                request.FinalStageDuration,
                request.DefaultLocationInterval,
                request.FinalLocationInterval,
                request.EnablePreyBoundaryPenalties,
                request.EnableHunterBoundaryPenalty);

            var result = await handler.Handle(command, ct);
            if (result is null) return Results.NotFound();
            return Results.Ok(result.Game);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("owner"))
        {
            return Results.Forbid();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ValidationProblem(ex);
        }
    }

    private static async Task<IResult> SetReady(
        Guid id,
        ClaimsPrincipal principal,
        IUserResolver userResolver,
        ICommandHandler<SetReadyCommand, SetReadyResult?> handler,
        CancellationToken ct)
    {
        var subjectId = principal.FindFirstValue("sub");
        if (subjectId is null) return Results.Unauthorized();
        var user = await userResolver.ResolveUser(subjectId, ct);
        if (user is null) return Results.Unauthorized();

        try
        {
            var result = await handler.Handle(new SetReadyCommand(id, user.UserId), ct);
            if (result is null) return Results.NotFound();
            return Results.Ok(result.Game);
        }
        catch (InvalidOperationException)
        {
            return Results.Forbid();
        }
        catch (Exception ex) when (ex is ArgumentException)
        {
            return ValidationProblem(ex);
        }
    }

    private static async Task<IResult> TagPlayer(
        Guid id,
        Guid participantId,
        ClaimsPrincipal principal,
        IUserResolver userResolver,
        ICommandHandler<TagPlayerCommand, TagPlayerResult?> handler,
        CancellationToken ct)
    {
        var subjectId = principal.FindFirstValue("sub");
        if (subjectId is null) return Results.Unauthorized();
        var user = await userResolver.ResolveUser(subjectId, ct);
        if (user is null) return Results.Unauthorized();

        try
        {
            var result = await handler.Handle(new TagPlayerCommand(id, user.UserId, participantId), ct);
            if (result is null) return Results.NotFound();
            return Results.NoContent();
        }
        catch (UnauthorizedAccessException)
        {
            return Results.Forbid();
        }
        catch (InvalidOperationException)
        {
            return Results.Conflict();
        }
        catch (ArgumentException)
        {
            return Results.NotFound();
        }
    }

    private static async Task<IResult> EndGame(
        Guid id,
        ClaimsPrincipal principal,
        IUserResolver userResolver,
        ICommandHandler<EndGameCommand, EndGameResult?> handler,
        CancellationToken ct)
    {
        var subjectId = principal.FindFirstValue("sub");
        if (subjectId is null) return Results.Unauthorized();
        var user = await userResolver.ResolveUser(subjectId, ct);
        if (user is null) return Results.Unauthorized();

        try
        {
            var result = await handler.Handle(new EndGameCommand(id, user.UserId), ct);
            return result is null ? Results.NotFound() : Results.NoContent();
        }
        catch (UnauthorizedAccessException)
        {
            return Results.Forbid();
        }
        catch (InvalidOperationException)
        {
            return Results.Conflict();
        }
    }

    private static async Task<IResult> LeaveGame(
        Guid id,
        ClaimsPrincipal principal,
        IUserResolver userResolver,
        ICommandHandler<LeaveGameCommand, LeaveGameResult?> handler,
        CancellationToken ct)
    {
        var subjectId = principal.FindFirstValue("sub");
        if (subjectId is null) return Results.Unauthorized();
        var user = await userResolver.ResolveUser(subjectId, ct);
        if (user is null) return Results.Unauthorized();

        try
        {
            var result = await handler.Handle(new LeaveGameCommand(id, user.UserId), ct);
            return result is null ? Results.NotFound() : Results.NoContent();
        }
        catch (UnauthorizedAccessException)
        {
            return Results.Forbid();
        }
        catch (InvalidOperationException)
        {
            return Results.Conflict();
        }
        catch (ArgumentException)
        {
            return Results.NotFound();
        }
    }

    private static async Task StreamLobbyEvents(
        Guid id,
        ClaimsPrincipal principal,
        IUserResolver userResolver,
        IGameRepository gameRepository,
        ILobbyEventBus eventBus,
        ILoggerFactory loggerFactory,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger("GameEndpoints.LobbyStream");

        var subjectId = principal.FindFirstValue("sub");
        if (subjectId is null)
        {
            logger.LogWarning("Lobby stream for game {GameId} rejected: no 'sub' claim (401)", id);
            httpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }
        var user = await userResolver.ResolveUser(subjectId, ct);
        if (user is null)
        {
            logger.LogWarning("Lobby stream for game {GameId} rejected: subject {SubjectId} did not resolve to a user (401)", id, subjectId);
            httpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        // Only participants of the game may subscribe to its lobby events (mirrors the
        // in-game stream). Prevents any authenticated user from spying on a lobby they are
        // not part of.
        var game = await gameRepository.GetByIdAsync(id, ct);
        if (game is null || !game.IsParticipant(user.UserId))
        {
            logger.LogWarning(
                "Lobby stream for game {GameId} rejected for user {UserId}: gameExists={GameExists}, isParticipant={IsParticipant} (403)",
                id, user.UserId, game is not null, game is not null && game.IsParticipant(user.UserId));
            httpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        httpContext.Response.ContentType = "text/event-stream";
        httpContext.Response.Headers.CacheControl = "no-cache";
        httpContext.Response.Headers.Append("X-Accel-Buffering", "no");

        // Flush an initial comment so the client's EventSource fires `onopen` immediately rather than
        // only on the first real event. This makes "did the connection actually open?" observable on
        // the device, and defeats intermediary response buffering that would otherwise hold the stream.
        await httpContext.Response.WriteAsync(": connected\n\n", ct);
        await httpContext.Response.Body.FlushAsync(ct);
        logger.LogInformation("Lobby stream opened for game {GameId}, user {UserId}", id, user.UserId);

        var eventCount = 0;
        try
        {
            await foreach (var evt in eventBus.Subscribe(id).WithCancellation(ct))
            {
                // The bus broadcasts one payload to every subscriber, so IsOwnerPlayer — which is per
                // recipient — is stamped here, where this connection's user is known.
                var payload = evt.Payload with { IsOwnerPlayer = evt.Payload.OwnerUserId == user.UserId };
                var personalized = evt with { Payload = payload };
                var json = System.Text.Json.JsonSerializer.Serialize(personalized, SseJsonOptions);
                await httpContext.Response.WriteAsync($"event: {evt.EventType}\ndata: {json}\n\n", ct);
                await httpContext.Response.Body.FlushAsync(ct);
                eventCount++;
                logger.LogInformation(
                    "Lobby stream wrote event '{EventType}' to user {UserId} for game {GameId}",
                    evt.EventType, user.UserId, id);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal: the client disconnected (navigated away / app backgrounded).
            logger.LogInformation("Lobby stream cancelled for game {GameId}, user {UserId}", id, user.UserId);
        }
        finally
        {
            logger.LogInformation(
                "Lobby stream closed for game {GameId}, user {UserId} after {EventCount} event(s)",
                id, user.UserId, eventCount);
        }
    }

    private static async Task StreamGameEvents(
        Guid id,
        ClaimsPrincipal principal,
        IUserResolver userResolver,
        IGameRepository gameRepository,
        IGameEventBus eventBus,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var subjectId = principal.FindFirstValue("sub");
        if (subjectId is null)
        {
            httpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }
        var user = await userResolver.ResolveUser(subjectId, ct);
        if (user is null)
        {
            httpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        var game = await gameRepository.GetByIdAsync(id, ct);
        if (game is null || !game.IsParticipant(user.UserId))
        {
            httpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        var isHunter = game.Hunter?.UserId == user.UserId;

        httpContext.Response.ContentType = "text/event-stream";
        httpContext.Response.Headers.CacheControl = "no-cache";
        httpContext.Response.Headers.Append("X-Accel-Buffering", "no");

        await foreach (var evt in eventBus.Subscribe(id).WithCancellation(ct))
        {
            // Prey location events go only to the hunter; skip for prey subscribers.
            if (evt is ParticipantLocatedEvent { ParticipantRole: "Prey" } && !isHunter)
                continue;

            var json = System.Text.Json.JsonSerializer.Serialize(evt, evt.GetType());
            await httpContext.Response.WriteAsync($"event: {evt.EventType}\ndata: {json}\n\n", ct);
            await httpContext.Response.Body.FlushAsync(ct);

            if (evt is GameEndedEvent)
            {
                eventBus.Complete(id);
                break;
            }
        }
        // participant-status-changed events are broadcast to all connected participants (hunters and preys)
        // via the default pass-through above — no filtering needed.
    }

    private static IResult ValidationProblem(Exception ex)
    {
        var key = (ex as ArgumentException)?.ParamName ?? "request";
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            [key] = [ex.Message]
        });
    }

    /// <summary>
    /// Returns a ProblemDetails carrying a stable <paramref name="code"/> in its extensions so the
    /// client can map the failure to a localized message without parsing the human-readable detail.
    /// </summary>
    private static IResult GameRuleProblem(string code, string detail, int statusCode)
        => Results.Problem(detail: detail, statusCode: statusCode, extensions: new Dictionary<string, object?>
        {
            ["code"] = code
        });
}
