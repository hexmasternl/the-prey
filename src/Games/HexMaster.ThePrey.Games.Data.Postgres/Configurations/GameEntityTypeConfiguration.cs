using HexMaster.ThePrey.Games.DomainModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HexMaster.ThePrey.Games.Data.Postgres.Configurations;

/// <summary>
/// Maps the <see cref="Game"/> aggregate to the relational schema. The lobby and participants are owned
/// collections (their own tables); each participant's current location is an owned value object mapped to
/// inline columns; the penalty and location history are owned collections serialised into JSON columns,
/// since they are only ever loaded as part of the aggregate and never queried in SQL.
/// </summary>
public sealed class GameEntityTypeConfiguration : IEntityTypeConfiguration<Game>
{
    public void Configure(EntityTypeBuilder<Game> builder)
    {
        builder.ToTable("Games");

        builder.HasKey(g => g.Id);
        builder.Property(g => g.Id).ValueGeneratedNever();
        builder.Property(g => g.GameCode).HasMaxLength(Game.GameCodeLength).IsRequired();
        builder.HasIndex(g => g.GameCode).IsUnique();
        builder.Property(g => g.PlayfieldId);
        builder.Property(g => g.OwnerUserId);
        builder.Property(g => g.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(g => g.StartedAt);
        builder.Property(g => g.CreatedAt);
        builder.Property(g => g.EndsAt);
        builder.Property(g => g.CleanUpAfter);
        builder.HasIndex(g => g.CleanUpAfter).HasDatabaseName("IX_Games_CleanUpAfter");
        builder.Property(g => g.DesignatedHunterUserId);
        builder.Property(g => g.CompletedAt);
        builder.Property(g => g.Outcome).HasConversion<int>().HasDefaultValue(GameOutcome.Undecided);

        // Computed, behaviour-only members must not be mapped.
        builder.Ignore(g => g.Hunter);
        builder.Ignore(g => g.Preys);
        builder.Ignore(g => g.ScheduledEndAt);

        builder.OwnsOne(g => g.Configuration, cfg =>
        {
            cfg.Property(c => c.GameDuration).HasColumnName("GameDuration");
            cfg.Property(c => c.HunterDelayTime).HasColumnName("HunterDelayTime");
            cfg.Property(c => c.FinalStageDuration).HasColumnName("FinalStageDuration");
            cfg.Property(c => c.DefaultLocationInterval).HasColumnName("DefaultLocationInterval");
            cfg.Property(c => c.FinalLocationInterval).HasColumnName("FinalLocationInterval");
            cfg.Property(c => c.EnablePreyBoundaryPenalties).HasColumnName("EnablePreyBoundaryPenalties");
            cfg.Property(c => c.EnableHunterBoundaryPenalty).HasColumnName("EnableHunterBoundaryPenalty");
        });
        builder.Navigation(g => g.Configuration).IsRequired();

        // Lobby — relational so membership can be filtered in SQL (see GameRepository.ListForUserAsync).
        builder.OwnsMany(g => g.Lobby, lobby =>
        {
            lobby.ToTable("LobbyPlayers");
            lobby.WithOwner().HasForeignKey("GameId");
            lobby.HasKey("GameId", "UserId");
            // UserId is a client-supplied key. Without this, EF's convention marks a Guid key as
            // store-generated (ValueGeneratedOnAdd) and then uses "is the key set?" to decide
            // add-vs-update: a populated UserId looks like an existing row, so adding a player to a
            // tracked game emits UPDATE (0 rows) instead of INSERT and throws a concurrency error.
            lobby.Property(p => p.UserId).ValueGeneratedNever();
            lobby.Property(p => p.DisplayName).HasMaxLength(256);
            lobby.Property(p => p.ProfilePictureUrl);
            lobby.Property(p => p.IsReady).HasColumnName("IsReady").HasDefaultValue(false);
        });
        builder.Navigation(g => g.Lobby).UsePropertyAccessMode(PropertyAccessMode.Field);

        // Participants — owned collection, accessed via the aggregate's backing field.
        builder.OwnsMany<GameParticipant>("_participants", participants =>
        {
            participants.ToTable("GameParticipants");
            participants.WithOwner().HasForeignKey("GameId");
            participants.HasKey("GameId", "UserId");
            // Client-supplied key — see the LobbyPlayers note above; ValueGeneratedNever keeps EF
            // from treating an added participant on a tracked game as an UPDATE instead of an INSERT.
            participants.Property(p => p.UserId).ValueGeneratedNever();
            participants.Property(p => p.Role).HasConversion<string>().HasMaxLength(16);
            participants.Property(p => p.State).HasConversion<string>().HasMaxLength(16).HasDefaultValue(PlayerState.Active);
            participants.Property(p => p.LastLocationAt).IsRequired(false);

            participants.OwnsOne(p => p.Location, location =>
            {
                location.Property(c => c.Latitude).HasColumnName("Latitude");
                location.Property(c => c.Longitude).HasColumnName("Longitude");
            });

            // History is never queried in SQL — serialise each collection into a single jsonb column,
            // mapped from the aggregate's private backing fields. The read-only accessors are ignored.
            participants.Ignore(p => p.Penalties);
            participants.Ignore(p => p.Locations);

            participants.Property<List<Penalty>>("_penalties")
                .HasConversion(JsonCollectionConverters.Converter<Penalty>(), JsonCollectionConverters.Comparer<Penalty>())
                .HasColumnType("jsonb")
                .HasColumnName("Penalties");

            participants.Property<List<LocationReading>>("_locations")
                .HasConversion(JsonCollectionConverters.Converter<LocationReading>(), JsonCollectionConverters.Comparer<LocationReading>())
                .HasColumnType("jsonb")
                .HasColumnName("Locations");
        });
        builder.Navigation("_participants").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
