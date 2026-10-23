using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HexMaster.ThePrey.Games.Data.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Games",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayfieldId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    GameDuration = table.Column<int>(type: "integer", nullable: false),
                    HunterDelayTime = table.Column<int>(type: "integer", nullable: false),
                    FinalStageDuration = table.Column<int>(type: "integer", nullable: false),
                    DefaultLocationInterval = table.Column<int>(type: "integer", nullable: false),
                    FinalLocationInterval = table.Column<int>(type: "integer", nullable: false),
                    EnablePreyBoundaryPenalties = table.Column<bool>(type: "boolean", nullable: false),
                    EnableHunterBoundaryPenalty = table.Column<bool>(type: "boolean", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Games", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GameParticipants",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    GameId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Latitude = table.Column<double>(type: "double precision", nullable: true),
                    Longitude = table.Column<double>(type: "double precision", nullable: true),
                    Locations = table.Column<string>(type: "jsonb", nullable: false),
                    Penalties = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameParticipants", x => new { x.GameId, x.UserId });
                    table.ForeignKey(
                        name: "FK_GameParticipants_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LobbyPlayers",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    GameId = table.Column<Guid>(type: "uuid", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ProfilePictureUrl = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LobbyPlayers", x => new { x.GameId, x.UserId });
                    table.ForeignKey(
                        name: "FK_LobbyPlayers_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GameParticipants");

            migrationBuilder.DropTable(
                name: "LobbyPlayers");

            migrationBuilder.DropTable(
                name: "Games");
        }
    }
}
