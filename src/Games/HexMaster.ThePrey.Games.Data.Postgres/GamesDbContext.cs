using HexMaster.ThePrey.Games.DomainModels;
using Microsoft.EntityFrameworkCore;

namespace HexMaster.ThePrey.Games.Data.Postgres;

public sealed class GamesDbContext : DbContext
{
    public GamesDbContext(DbContextOptions<GamesDbContext> options) : base(options)
    {
    }

    public DbSet<Game> Games => Set<Game>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(GamesDbContext).Assembly);
    }
}
