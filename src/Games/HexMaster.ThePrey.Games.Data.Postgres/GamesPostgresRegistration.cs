using HexMaster.ThePrey.Games;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace HexMaster.ThePrey.Games.Data.Postgres;

public static class GamesPostgresRegistration
{
    /// <summary>The Aspire connection name; must match the PostgreSQL database resource modelled in the AppHost.</summary>
    public const string ConnectionName = "games";

    public static IHostApplicationBuilder AddGamesPostgres(this IHostApplicationBuilder builder)
    {
        builder.AddNpgsqlDbContext<GamesDbContext>(ConnectionName);
        builder.Services.AddScoped<IGameRepository, GameRepository>();
        return builder;
    }
}
