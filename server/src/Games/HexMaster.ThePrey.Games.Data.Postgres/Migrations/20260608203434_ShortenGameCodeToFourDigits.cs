using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HexMaster.ThePrey.Games.Data.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class ShortenGameCodeToFourDigits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Existing rows hold 8-digit codes that would both overflow varchar(4) and break the
            // unique index. Reassign each game a distinct 4-digit code (row-number based, so it stays
            // unique for up to 10,000 games) while the column is still varchar(8), then shrink it.
            migrationBuilder.Sql(
                """
                WITH numbered AS (
                    SELECT "Id", (ROW_NUMBER() OVER (ORDER BY "Id") - 1) AS rn
                    FROM "Games"
                )
                UPDATE "Games" g
                SET "GameCode" = LPAD((numbered.rn % 10000)::TEXT, 4, '0')
                FROM numbered
                WHERE g."Id" = numbered."Id";
                """);

            migrationBuilder.AlterColumn<string>(
                name: "GameCode",
                table: "Games",
                type: "character varying(4)",
                maxLength: 4,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(8)",
                oldMaxLength: 8);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "GameCode",
                table: "Games",
                type: "character varying(8)",
                maxLength: 8,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(4)",
                oldMaxLength: 4);
        }
    }
}
