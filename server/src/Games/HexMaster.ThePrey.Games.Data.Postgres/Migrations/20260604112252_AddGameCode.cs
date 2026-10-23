using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HexMaster.ThePrey.Games.Data.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddGameCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GameCode",
                table: "Games",
                type: "character varying(8)",
                maxLength: 8,
                nullable: false,
                defaultValue: "");

            // Backfill pre-existing games with random 8-digit codes so the unique index can be created.
            migrationBuilder.Sql(
                """
                UPDATE "Games"
                SET "GameCode" = LPAD(FLOOR(RANDOM() * 100000000)::TEXT, 8, '0')
                WHERE "GameCode" = '';
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Games_GameCode",
                table: "Games",
                column: "GameCode",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Games_GameCode",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "GameCode",
                table: "Games");
        }
    }
}
