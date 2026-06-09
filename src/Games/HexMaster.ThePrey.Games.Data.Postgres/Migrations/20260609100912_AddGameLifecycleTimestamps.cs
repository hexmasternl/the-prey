using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HexMaster.ThePrey.Games.Data.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddGameLifecycleTimestamps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CleanUpAfter",
                table: "Games",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "NOW() + INTERVAL '48 hours'");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAt",
                table: "Games",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "NOW()");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "EndsAt",
                table: "Games",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Games_CleanUpAfter",
                table: "Games",
                column: "CleanUpAfter");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Games_CleanUpAfter",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "CleanUpAfter",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "EndsAt",
                table: "Games");
        }
    }
}
