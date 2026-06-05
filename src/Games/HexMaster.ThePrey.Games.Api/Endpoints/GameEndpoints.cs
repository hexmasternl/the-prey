using System.Security.Claims;
using HexMaster.ThePrey.Core;
using HexMaster.ThePrey.Games.Abstractions.DataTransferObjects;
using HexMaster.ThePrey.Games.Features.CreateGame;
using HexMaster.ThePrey.Games.Features.GetActiveGame;
using HexMaster.ThePrey.Games.Features.GetGame;
using HexMaster.ThePrey.Games.Features.GetGameState;
using HexMaster.ThePrey.Games.Features.JoinGame;
using HexMaster.ThePrey.Games.Features.ListGames;
using HexMaster.ThePrey.Games.Features.RecordPlayerLocation;
using HexMaster.ThePrey.Games.Features.SetHunter;
using HexMaster.ThePrey.Games.Features.StartGame;
using Microsoft.AspNetCore.Mvc;

namespace HexMaster.ThePrey.Games.Api.Endpoints;

public static class GameEndpoints
{
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
            .Produces<ActiveGameDto>()
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status401Unauthorized);

        return app;
    }

    private static async Task<IResult> CreateGame(
        [FromBody] CreateGameRequest request,
        ClaimsPrincipal principal,
        ICommandHandler<CreateGameCommand, CreateGameResult> handler,
        CancellationToken ct)
    {
        if (GetUserId(principal) is not { } ownerId)
            return Results.Unauthorized();

        var command = new CreateGameCommand(
            ownerId,
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
        ICommandHandler<JoinGameCommand, JoinGameResult?> handler,
        CancellationToken ct)
    {
        if (GetUserId(principal) is not { } userId)
            return Results.Unauthorized();

        try
        {
            var result = await handler.Handle(new JoinGameCommand(id, userId, request.DisplayName, request.ProfilePictureUrl), ct);
            return result is null ? Results.NotFound() : Results.Ok(result.Game);
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
        ICommandHandler<StartGameCommand, StartGameResult?> handler,
        CancellationToken ct)
    {
        if (GetUserId(principal) is not { } userId)
            return Results.Unauthorized();

        try
        {
            var result = await handler.Handle(new StartGameCommand(id, userId, request.HunterUserId), ct);
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
        ICommandHandler<SetHunterCommand, SetHunterResult?> handler,
        CancellationToken ct)
    {
        if (GetUserId(principal) is not { } userId)
            return Results.Unauthorized();

        try
        {
            var result = await handler.Handle(new SetHunterCommand(id, userId, request.NewHunterUserId), ct);
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
        ICommandHandler<RecordPlayerLocationCommand, RecordPlayerLocationResult?> handler,
        CancellationToken ct)
    {
        if (GetUserId(principal) is not { } userId)
            return Results.Unauthorized();

        try
        {
            var result = await handler.Handle(
                new RecordPlayerLocationCommand(id, userId, request.Latitude, request.Longitude, request.RecordedAt, request.Accuracy), ct);
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
        IQueryHandler<GetGameQuery, GameDto?> handler,
        CancellationToken ct)
    {
        if (GetUserId(principal) is null)
            return Results.Unauthorized();

        var game = await handler.Handle(new GetGameQuery(id), ct);
        return game is not null ? Results.Ok(game) : Results.NotFound();
    }

    private static async Task<IResult> GetGameState(
        Guid id,
        ClaimsPrincipal principal,
        IQueryHandler<GetGameStateQuery, GameStateDto?> handler,
        CancellationToken ct)
    {
        if (GetUserId(principal) is not { } userId)
            return Results.Unauthorized();

        var state = await handler.Handle(new GetGameStateQuery(id, userId), ct);
        return state is not null ? Results.Ok(state) : Results.NotFound();
    }

    private static async Task<IResult> ListGames(
        ClaimsPrincipal principal,
        IQueryHandler<ListGamesQuery, IReadOnlyList<GameSummaryDto>> handler,
        CancellationToken ct)
    {
        if (GetUserId(principal) is not { } userId)
            return Results.Unauthorized();

        var games = await handler.Handle(new ListGamesQuery(userId), ct);
        return Results.Ok(games);
    }

    private static async Task<IResult> GetActiveGame(
        ClaimsPrincipal principal,
        IQueryHandler<GetActiveGameQuery, ActiveGameDto?> handler,
        CancellationToken ct)
    {
        if (GetUserId(principal) is not { } userId)
            return Results.Unauthorized();

        var active = await handler.Handle(new GetActiveGameQuery(userId), ct);
        return active is not null ? Results.Ok(active) : Results.NotFound();
    }

    private static Guid? GetUserId(ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue("sub"), out var userId) ? userId : null;

    private static IResult ValidationProblem(Exception ex)
    {
        var key = (ex as ArgumentException)?.ParamName ?? "request";
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            [key] = [ex.Message]
        });
    }
}
