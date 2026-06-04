using HexMaster.ThePrey.Core;
using HexMaster.ThePrey.Users.Abstractions.DataTransferObjects;
using HexMaster.ThePrey.Users.Features.CreateUser;
using HexMaster.ThePrey.Users.Features.GetUser;
using HexMaster.ThePrey.Users.Features.UpdateUser;
using HexMaster.ThePrey.Users.Features.UpdateUserSettings;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using System.Security.Claims;

namespace HexMaster.ThePrey.Users.Api.Endpoints;

public static class UserEndpoints
{
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/users")
            .WithTags("Users")
            .RequireAuthorization();

        group.MapPost("/", CreateUser)
            .WithName("CreateUser")
            .Produces<UserDto>(StatusCodes.Status201Created)
            .Produces<UserDto>(StatusCodes.Status200OK)
            .ProducesValidationProblem();

        group.MapGet("/me", GetCurrentUser)
            .WithName("GetCurrentUser")
            .Produces<UserDto>()
            .Produces(StatusCodes.Status404NotFound);

        group.MapPut("/me", UpdateCurrentUser)
            .WithName("UpdateCurrentUser")
            .Produces<UserDto>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPut("/settings", UpdateSettings)
            .WithName("UpdateUserSettings")
            .Produces<UserDto>()
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> CreateUser(
        [FromBody] CreateUserRequest request,
        ClaimsPrincipal principal,
        ICommandHandler<CreateUserCommand, CreateUserResult> handler,
        CancellationToken ct)
    {
        var subjectId = GetSubjectId(principal);
        if (subjectId is null)
            return Results.Unauthorized();

        if (string.IsNullOrWhiteSpace(request.EmailAddress))
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.EmailAddress)] = ["Email address is required."]
            });

        var command = new CreateUserCommand(
            subjectId,
            request.FirstName,
            request.LastName,
            request.EmailAddress,
            request.IsEmailVerified,
            request.PreferredLanguage);

        var result = await handler.Handle(command, ct);

        return result.WasCreated
            ? Results.Created("/users/me", result.User)
            : Results.Ok(result.User);
    }

    private static async Task<IResult> GetCurrentUser(
        ClaimsPrincipal principal,
        IQueryHandler<GetUserQuery, UserDto?> handler,
        CancellationToken ct)
    {
        var subjectId = GetSubjectId(principal);
        if (subjectId is null)
            return Results.Unauthorized();

        var user = await handler.Handle(new GetUserQuery(subjectId), ct);

        return user is not null ? Results.Ok(user) : Results.NotFound();
    }

    private static async Task<IResult> UpdateCurrentUser(
        [FromBody] UpdateUserRequest request,
        ClaimsPrincipal principal,
        ICommandHandler<UpdateUserCommand, UserDto> handler,
        CancellationToken ct)
    {
        var subjectId = GetSubjectId(principal);
        if (subjectId is null)
            return Results.Unauthorized();

        if (string.IsNullOrWhiteSpace(request.DisplayName))
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.DisplayName)] = ["Display name is required."]
            });

        if (string.IsNullOrWhiteSpace(request.PreferredLanguage))
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.PreferredLanguage)] = ["Preferred language is required."]
            });

        try
        {
            var command = new UpdateUserCommand(
                subjectId,
                request.FirstName,
                request.LastName,
                request.DisplayName,
                request.PreferredLanguage);

            var result = await handler.Handle(command, ct);
            return Results.Ok(result);
        }
        catch (InvalidOperationException)
        {
            return Results.NotFound();
        }
    }

    private static async Task<IResult> UpdateSettings(
        [FromBody] UpdateUserSettingsRequest request,
        ClaimsPrincipal principal,
        ICommandHandler<UpdateUserSettingsCommand, UserDto> handler,
        CancellationToken ct)
    {
        var subjectId = GetSubjectId(principal);
        if (subjectId is null)
            return Results.Unauthorized();

        try
        {
            var command = new UpdateUserSettingsCommand(subjectId, request.Callsign, request.PreferredLanguage);
            var result = await handler.Handle(command, ct);
            return Results.Ok(result);
        }
        catch (ArgumentException ex)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [ex.ParamName ?? "request"] = [ex.Message]
            });
        }
        catch (InvalidOperationException)
        {
            return Results.NotFound();
        }
    }

    private static string? GetSubjectId(ClaimsPrincipal principal) =>
        principal.FindFirstValue("sub");
}
