using HexMaster.ThePrey.PlayFields.Abstractions.DataTransferObjects;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace HexMaster.ThePrey.PlayFields.Api.Endpoints;

public static class InternalPlayFieldEndpoints
{
    public static IEndpointRouteBuilder MapInternalPlayFieldEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/internal/playfields")
            .WithTags("Internal");

        group.MapGet("/{id:guid}", GetPlayField)
            .WithName("InternalGetPlayField")
            .AddEndpointFilter<DaprApiTokenEndpointFilter>()
            .Produces<PlayFieldDto>()
            .Produces(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> GetPlayField(
        Guid id,
        IPlayFieldRepository repository,
        CancellationToken ct)
    {
        var playField = await repository.GetByIdAsync(id, ct);
        if (playField is null)
            return Results.NotFound();

        var dto = new PlayFieldDto(
            playField.Id,
            playField.Name,
            playField.OwnerId,
            playField.IsPublic,
            playField.Points.Select(p => new GpsCoordinateDto(p.Latitude, p.Longitude)).ToList(),
            playField.LastModifiedOn,
            playField.CenterCoordinates is { } c ? new GpsCoordinateDto(c.Latitude, c.Longitude) : null);

        return Results.Ok(dto);
    }
}
