using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HexMaster.ThePrey.Games.Data.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class UnifyLobbyIntoParticipants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Rename DesignatedHunterUserId → HunterUserId on Games
            migrationBuilder.RenameColumn(
                name: "DesignatedHunterUserId",
                table: "Games",
                newName: "HunterUserId");

            // 2. Add DisplayName to GameParticipants (absorbed from LobbyPlayers)
            //    Existing rows get an empty-string default; a follow-up data migration can populate them.
            migrationBuilder.AddColumn<string>(
                name: "DisplayName",
                table: "GameParticipants",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "");

            // 3. Add ProfilePictureUrl to GameParticipants (nullable)
            migrationBuilder.AddColumn<string>(
                name: "ProfilePictureUrl",
                table: "GameParticipants",
                type: "text",
                nullable: true);

            // 4. Add IsReady to GameParticipants (default false)
            migrationBuilder.AddColumn<bool>(
                name: "IsReady",
                table: "GameParticipants",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // 5. Drop the Role column from GameParticipants
            migrationBuilder.DropColumn(
                name: "Role",
                table: "GameParticipants");

            // 6. Drop the LobbyPlayers table (its data is no longer needed — lobby and participants
            //    are now the same collection)
            migrationBuilder.DropTable(
                name: "LobbyPlayers");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Recreate LobbyPlayers
            migrationBuilder.CreateTable(
                name: "LobbyPlayers",
                columns: table => new
                {
                    GameId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    IsReady = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
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

            // Restore Role column
            migrationBuilder.AddColumn<string>(
                name: "Role",
                table: "GameParticipants",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Prey");

            // Remove new columns from GameParticipants
            migrationBuilder.DropColumn(name: "DisplayName", table: "GameParticipants");
            migrationBuilder.DropColumn(name: "ProfilePictureUrl", table: "GameParticipants");
            migrationBuilder.DropColumn(name: "IsReady", table: "GameParticipants");

            // Rename HunterUserId back to DesignatedHunterUserId
            migrationBuilder.RenameColumn(
                name: "HunterUserId",
                table: "Games",
                newName: "DesignatedHunterUserId");
        }
    }
}
