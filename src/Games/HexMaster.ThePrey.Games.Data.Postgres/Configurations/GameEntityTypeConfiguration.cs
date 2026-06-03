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
        builder.Property(g => g.PlayfieldId);
        builder.Property(g => g.OwnerUserId);
        builder.Property(g => g.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(g => g.StartedAt);

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
            lobby.Property(p => p.UserId);
            lobby.Property(p => p.DisplayName).HasMaxLength(256);
            lobby.Property(p => p.ProfilePictureUrl);
        });
        builder.Navigation(g => g.Lobby).UsePropertyAccessMode(PropertyAccessMode.Field);

        // Participants — owned collection, accessed via the aggregate's backing field.
        builder.OwnsMany<GameParticipant>("_participants", participants =>
        {
            participants.ToTable("GameParticipants");
            participants.WithOwner().HasForeignKey("GameId");
            participants.HasKey("GameId", "UserId");
            participants.Property(p => p.UserId);
            participants.Property(p => p.Role).HasConversion<string>().HasMaxLength(16);

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
