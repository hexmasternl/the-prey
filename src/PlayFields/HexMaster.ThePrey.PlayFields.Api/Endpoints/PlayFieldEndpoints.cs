using HexMaster.ThePrey.Core;
using HexMaster.ThePrey.PlayFields.Abstractions.DataTransferObjects;
using HexMaster.ThePrey.PlayFields.Features.CreatePlayField;
using HexMaster.ThePrey.PlayFields.Features.DeletePlayField;
using HexMaster.ThePrey.PlayFields.Features.GetPlayField;
using HexMaster.ThePrey.PlayFields.Features.ListPlayFields;
using HexMaster.ThePrey.PlayFields.Features.SearchPublicPlayFields;
using HexMaster.ThePrey.PlayFields.Features.UpsertPlayField;
using HexMaster.ThePrey.Users.Integration;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using System.Security.Claims;

namespace HexMaster.ThePrey.PlayFields.Api.Endpoints;

public static class PlayFieldEndpoints
{
    public static IEndpointRouteBuilder MapPlayFieldEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/playfields")
            .WithTags("PlayFields")
            .RequireAuthorization();

        group.MapPost("/", CreatePlayField)
            .WithName("CreatePlayField")
            .Produces<PlayFieldDto>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status401Unauthorized);

        group.MapPut("/{id:guid}", UpsertPlayField)
            .WithName("UpsertPlayField")
            .Produces<PlayFieldDto>(StatusCodes.Status200OK)
            .Produces<PlayFieldDto>(StatusCodes.Status201Created)
            .Produces<PlayFieldDto>(StatusCodes.Status409Conflict)
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        group.MapGet("/{id:guid}", GetPlayField)
            .WithName("GetPlayField")
            .Produces<PlayFieldDto>()
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/", ListPlayFields)
            .WithName("ListPlayFields")
            .Produces<IReadOnlyList<PlayFieldSummaryDto>>();

        group.MapGet("/public", SearchPublicPlayFields)
            .WithName("SearchPublicPlayFields")
            .Produces<IReadOnlyList<PlayFieldSummaryDto>>()
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status401Unauthorized);

        group.MapDelete("/{id:guid}", DeletePlayField)
            .WithName("DeletePlayField")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status401Unauthorized);

        return app;
    }

    private static async Task<IResult> CreatePlayField(
        [FromBody] CreatePlayFieldRequest request,
        ClaimsPrincipal principal,
        IUserResolver userResolver,
        ICommandHandler<CreatePlayFieldCommand, CreatePlayFieldResult> handler,
        CancellationToken ct)
    {
        var subjectId = principal.FindFirstValue("sub");
        if (subjectId is null) return Results.Unauthorized();

        var user = await userResolver.ResolveUser(subjectId, ct);
        if (user is null) return Results.Unauthorized();

        if (string.IsNullOrWhiteSpace(request.Name))
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.Name)] = ["Name is required."]
            });

        if (request.Points is null || request.Points.Count < 3)
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.Points)] = ["A play field requires at least 3 points."]
            });

        try
        {
            var command = new CreatePlayFieldCommand(user.UserId, request.Name, request.IsPublic, request.Points);
            var result = await handler.Handle(command, ct);
            return Results.Created($"/playfields/{result.PlayField.Id}", result.PlayField);
        }
        catch (ArgumentException ex)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [ex.ParamName ?? "request"] = [ex.Message]
            });
        }
    }

    private static async Task<IResult> GetPlayField(
        Guid id,
        ClaimsPrincipal principal,
        IUserResolver userResolver,
        IQueryHandler<GetPlayFieldQuery, PlayFieldDto?> handler,
        CancellationToken ct)
    {
        var subjectId = principal.FindFirstValue("sub");
        if (subjectId is null) return Results.Unauthorized();

        var user = await userResolver.ResolveUser(subjectId, ct);
        if (user is null) return Results.Unauthorized();

        var playField = await handler.Handle(new GetPlayFieldQuery(id, user.UserId), ct);

        return playField is not null ? Results.Ok(playField) : Results.NotFound();
    }

    private static async Task<IResult> ListPlayFields(
        ClaimsPrincipal principal,
        IUserResolver userResolver,
        IQueryHandler<ListPlayFieldsQuery, IReadOnlyList<PlayFieldSummaryDto>> handler,
        CancellationToken ct)
    {
        var subjectId = principal.FindFirstValue("sub");
        if (subjectId is null) return Results.Unauthorized();

        var user = await userResolver.ResolveUser(subjectId, ct);
        if (user is null) return Results.Unauthorized();

        var playFields = await handler.Handle(new ListPlayFieldsQuery(user.UserId), ct);

        return Results.Ok(playFields);
    }

    private static async Task<IResult> UpsertPlayField(
        Guid id,
        [FromBody] UpsertPlayFieldRequest request,
        ClaimsPrincipal principal,
        IUserResolver userResolver,
        ICommandHandler<UpsertPlayFieldCommand, UpsertPlayFieldResult> handler,
        CancellationToken ct)
    {
        var subjectId = principal.FindFirstValue("sub");
        if (subjectId is null) return Results.Unauthorized();

        var user = await userResolver.ResolveUser(subjectId, ct);
        if (user is null) return Results.Unauthorized();

        if (string.IsNullOrWhiteSpace(request.Name))
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.Name)] = ["Name is required."]
            });

        if (request.Points is null || request.Points.Count < 3)
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.Points)] = ["A play field requires at least 3 points."]
            });

        try
        {
            var command = new UpsertPlayFieldCommand(id, user.UserId, request.Name, request.IsPublic, request.Points, request.LastUpdatedOn);
            var result = await handler.Handle(command, ct);

            return result switch
            {
                UpsertPlayFieldResult.Created c => Results.Created($"/playfields/{id}", c.PlayField),
                UpsertPlayFieldResult.Updated u => Results.Ok(u.PlayField),
                UpsertPlayFieldResult.Conflict conflict => Results.Conflict(conflict.CurrentPlayField),
                UpsertPlayFieldResult.Forbidden => Results.Forbid(),
                _ => Results.StatusCode(500)
            };
        }
        catch (ArgumentException ex)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [ex.ParamName ?? "request"] = [ex.Message]
            });
        }
    }

    private static async Task<IResult> SearchPublicPlayFields(
        [FromQuery(Name = "q")] string? query,
        IQueryHandler<SearchPublicPlayFieldsQuery, IReadOnlyList<PlayFieldSummaryDto>> handler,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Trim().Length < SearchPublicPlayFieldsQuery.MinimumSearchLength)
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["q"] = [$"The search text must be at least {SearchPublicPlayFieldsQuery.MinimumSearchLength} characters long."]
            });

        try
        {
            var playFields = await handler.Handle(new SearchPublicPlayFieldsQuery(query.Trim()), ct);
            return Results.Ok(playFields);
        }
        catch (ArgumentException ex)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["q"] = [ex.Message]
            });
        }
    }

    private static async Task<IResult> DeletePlayField(
        Guid id,
        ClaimsPrincipal principal,
        IUserResolver userResolver,
        ICommandHandler<DeletePlayFieldCommand, DeletePlayFieldResult> handler,
        CancellationToken ct)
    {
        var subjectId = principal.FindFirstValue("sub");
        if (subjectId is null) return Results.Unauthorized();

        var user = await userResolver.ResolveUser(subjectId, ct);
        if (user is null) return Results.Unauthorized();

        var command = new DeletePlayFieldCommand(id, user.UserId);
        var result = await handler.Handle(command, ct);

        return result switch
        {
            DeletePlayFieldResult.Success => Results.NoContent(),
            DeletePlayFieldResult.NotFound => Results.NotFound(),
            DeletePlayFieldResult.Forbidden => Results.Forbid(),
            _ => Results.StatusCode(500)
        };
    }
}
