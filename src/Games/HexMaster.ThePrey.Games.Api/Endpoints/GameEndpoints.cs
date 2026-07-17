using System.Diagnostics;
using System.Security.Claims;
using Azure.Messaging.WebPubSub;
using HexMaster.ThePrey.Core;
using HexMaster.ThePrey.Games.Abstractions.DataTransferObjects;
using HexMaster.ThePrey.Games.DomainModels;
using HexMaster.ThePrey.Games.Observability;
using HexMaster.ThePrey.Games.Features.CheckAppVersion;
using HexMaster.ThePrey.Games.Features.CreateGame;
using HexMaster.ThePrey.Games.Features.EndGame;
using HexMaster.ThePrey.Games.Features.GetActiveGame;
using HexMaster.ThePrey.Games.Features.GetGame;
using HexMaster.ThePrey.Games.Features.GetGameState;
using HexMaster.ThePrey.Games.Features.GetGameStatus;
using HexMaster.ThePrey.Games.Features.GetTagCandidates;
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
using HexMaster.ThePrey.Users.Integration;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace HexMaster.ThePrey.Games.Api.Endpoints;

public static class GameEndpoints
{
    /// <summary>
    /// Lifetime of a minted Web PubSub client access token. The client re-requests a fresh token on
    /// every (re)connect, so this only needs to comfortably outlast a single connection attempt.
    /// </summary>
    private static readonly TimeSpan NotificationsTokenLifetime = TimeSpan.FromHours(1);

    /// <summary>
    /// Activity source for spans emitted by these endpoints. Created from the public Games meter/source
    /// name (the module's internal <c>GameActivitySource</c> is not visible across the assembly boundary);
    /// it is registered for export in <c>Program.cs</c> via <c>AddSource</c>.
    /// </summary>
    private static readonly ActivitySource ActivitySource = new(GameObservabilityConstants.ActivitySourceName);

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

        group.MapGet("/{id:guid}/notifications/token", GetNotificationsToken)
            .WithName("GetGameNotificationsToken")
            .Produces<GameNotificationConnectionDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

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

        group.MapGet("/{id:guid}/tag-candidates", GetTagCandidates)
            .WithName("GetTagCandidates")
            .Produces<TagCandidatesDto>()
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status401Unauthorized);

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

        // Version gate — mapped outside the authenticated group so it works regardless of auth
        // state (a token problem must never mask a required-update signal, and the client checks
        // this before/independently of login).
        app.MapPost("/games/version-checker", CheckAppVersion)
            .WithName("CheckAppVersion")
            .WithTags("Games")
            .AllowAnonymous()
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status409Conflict)
            .ProducesValidationProblem();

        return app;
    }

    private static async Task<IResult> CheckAppVersion(
        [FromBody] CheckAppVersionRequest request,
        IQueryHandler<CheckAppVersionQuery, AppVersionCheckResult> handler,
        CancellationToken ct)
    {
        try
        {
            var result = await handler.Handle(new CheckAppVersionQuery(request.CurrentVersion), ct);
            return result == AppVersionCheckResult.UpdateRequired
                ? Results.Conflict()
                : Results.NoContent();
        }
        catch (ArgumentException ex)
        {
            return ValidationProblem(ex);
        }
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

    /// <summary>
    /// Validates that the caller is a member of the game (owner or participant) and, if so, mints a
    /// short-lived, group-scoped Web PubSub client access URL. The returned URL embeds the access token
    /// and the role that lets the client join only this game's group, so the client opens a native
    /// WebSocket to it and subscribes to the <c>{gameId}</c> group to receive the game's real-time events.
    /// </summary>
    private static async Task<IResult> GetNotificationsToken(
        Guid id,
        ClaimsPrincipal principal,
        IUserResolver userResolver,
        IGameRepository gameRepository,
        WebPubSubServiceClient webPubSubClient,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger("GameEndpoints.NotificationsToken");

        using var activity = ActivitySource.StartActivity("Games.NotificationsToken");
        activity?.SetTag("game.id", id);

        var subjectId = principal.FindFirstValue("sub");
        if (subjectId is null) return Results.Unauthorized();
        var user = await userResolver.ResolveUser(subjectId, ct);
        if (user is null) return Results.Unauthorized();

        activity?.SetTag("user.id", user.UserId);

        var game = await gameRepository.GetByIdAsync(id, ct);
        if (game is null || !game.IsVisibleTo(user.UserId))
        {
            activity?.SetTag("notifications.outcome", "forbidden");
            logger.LogWarning(
                "Web PubSub token for game {GameId} denied for user {UserId}: gameExists={GameExists} (403)",
                id, user.UserId, game is not null);
            return Results.Forbid();
        }

        try
        {
            // Grant only the join/leave role scoped to this game's group; the client subscribes to it
            // explicitly after connecting. No groups are pre-joined, so the token cannot be used to
            // listen in on any other game.
            var roles = new[] { $"webpubsub.joinLeaveGroup.{id}" };
            var uri = webPubSubClient.GetClientAccessUri(
                expiresAfter: NotificationsTokenLifetime,
                userId: subjectId,
                roles: roles,
                groups: null);

            activity?.SetTag("notifications.outcome", "granted");
            logger.LogInformation("Issued Web PubSub access for user {UserId} on game {GameId}.", user.UserId, id);
            return Results.Ok(new GameNotificationConnectionDto(uri.AbsoluteUri));
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            logger.LogError(ex, "Failed to mint Web PubSub access for user {UserId} on game {GameId}.", user.UserId, id);
            throw;
        }
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

    private static async Task<IResult> GetTagCandidates(
        Guid id,
        ClaimsPrincipal principal,
        IUserResolver userResolver,
        IQueryHandler<GetTagCandidatesQuery, TagCandidatesDto?> handler,
        CancellationToken ct)
    {
        var subjectId = principal.FindFirstValue("sub");
        if (subjectId is null) return Results.Unauthorized();
        var user = await userResolver.ResolveUser(subjectId, ct);
        if (user is null) return Results.Unauthorized();

        try
        {
            var candidates = await handler.Handle(new GetTagCandidatesQuery(id, user.UserId), ct);
            return candidates is not null ? Results.Ok(candidates) : Results.NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return Results.Forbid();
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
