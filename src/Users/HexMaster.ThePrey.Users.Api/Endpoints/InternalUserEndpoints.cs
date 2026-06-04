using HexMaster.ThePrey.Core;
using HexMaster.ThePrey.Users.Abstractions.DataTransferObjects;
using HexMaster.ThePrey.Users.Features.ResolveUserBySubject;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace HexMaster.ThePrey.Users.Api.Endpoints;

public static class InternalUserEndpoints
{
    public static IEndpointRouteBuilder MapInternalUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/internal/users")
            .WithTags("Internal");

        group.MapGet("/{subjectId}", ResolveUserBySubjectId)
            .WithName("ResolveUserBySubjectId")
            .AddEndpointFilter<DaprApiTokenEndpointFilter>()
            .Produces<UserDto>()
            .Produces(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> ResolveUserBySubjectId(
        string subjectId,
        IQueryHandler<ResolveUserBySubjectQuery, UserDto?> handler,
        CancellationToken ct)
    {
        var user = await handler.Handle(new ResolveUserBySubjectQuery(subjectId), ct);
        return user is not null ? Results.Ok(user) : Results.NotFound();
    }
}
