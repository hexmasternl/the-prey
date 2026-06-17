using System.Globalization;
using HexMaster.ThePrey.Core;
using HexMaster.ThePrey.Games.Abstractions.DataTransferObjects;
using HexMaster.ThePrey.Games.Features.ExportGames;

namespace HexMaster.ThePrey.Games.Api.Endpoints;

/// <summary>
/// Public, unauthenticated export endpoint. Intentionally has no <c>.RequireAuthorization()</c>.
/// <c>.AllowAnonymous()</c> is added explicitly to override any global auth fallback policy.
/// </summary>
public static class GameExportEndpoints
{
    public static IEndpointRouteBuilder MapGameExportEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/games/export")
            .WithTags("Games")
            .AllowAnonymous();

        group.MapGet("/today", ExportGamesToday)
            .WithName("ExportGamesPlayedToday")
            .Produces<IReadOnlyList<GameExportDto>>()
            .AllowAnonymous();

        return app;
    }

    private static async Task<IResult> ExportGamesToday(
        IQueryHandler<ExportGamesQuery, IReadOnlyList<GameExportDto>> handler,
        string? date,
        CancellationToken ct)
    {
        DateTimeOffset from;

        if (date is not null)
        {
            if (!DateOnly.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["date"] = [$"'{date}' is not a valid date. Expected format: yyyy-MM-dd."]
                });
            }

            from = new DateTimeOffset(parsed.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        }
        else
        {
            var today = DateTimeOffset.UtcNow.Date;
            from = new DateTimeOffset(today, TimeSpan.Zero);
        }

        var to = from.AddDays(1);
        var result = await handler.Handle(new ExportGamesQuery(from, to), ct);
        return Results.Ok(result);
    }
}
