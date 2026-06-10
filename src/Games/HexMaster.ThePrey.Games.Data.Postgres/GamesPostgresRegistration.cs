using HexMaster.ThePrey.Games;
using HexMaster.ThePrey.Games.Data.Postgres.LeaderElection;
using HexMaster.ThePrey.Games.LeaderElection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HexMaster.ThePrey.Games.Data.Postgres;

public static class GamesPostgresRegistration
{
    /// <summary>The Aspire connection name; must match the PostgreSQL database resource modelled in the AppHost.</summary>
    public const string ConnectionName = "games";

    public static IHostApplicationBuilder AddGamesPostgres(this IHostApplicationBuilder builder)
    {
        builder.AddNpgsqlDbContext<GamesDbContext>(ConnectionName);
        builder.Services.AddScoped<IGameRepository, GameRepository>();

        // The sweep leader lock needs a dedicated, long-lived connection (not an EF pooled one), so it
        // owns connections built from the same connection string.
        builder.Services.AddSingleton<ILeaderElection>(sp =>
        {
            var connectionString = sp.GetRequiredService<IConfiguration>().GetConnectionString(ConnectionName)
                ?? throw new InvalidOperationException($"Connection string '{ConnectionName}' was not found.");
            return new PostgresAdvisoryLockLeaderElection(
                connectionString,
                sp.GetRequiredService<ILogger<PostgresAdvisoryLockLeaderElection>>());
        });

        return builder;
    }
}
