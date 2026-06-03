using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace HexMaster.ThePrey.Games.Data.Postgres;

/// <summary>
/// Design-time factory so <c>dotnet ef migrations</c> can build the model without the Aspire AppHost.
/// The connection string here is only used for scaffolding; the runtime connection comes from Aspire.
/// </summary>
public sealed class GamesDbContextFactory : IDesignTimeDbContextFactory<GamesDbContext>
{
    public GamesDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<GamesDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=games;Username=postgres;Password=postgres")
            .Options;

        return new GamesDbContext(options);
    }
}
