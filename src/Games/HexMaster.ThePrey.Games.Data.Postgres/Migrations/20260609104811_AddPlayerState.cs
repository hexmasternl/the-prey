using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HexMaster.ThePrey.Games.Data.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddPlayerState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastLocationAt",
                table: "GameParticipants",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "State",
                table: "GameParticipants",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Active");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastLocationAt",
                table: "GameParticipants");

            migrationBuilder.DropColumn(
                name: "State",
                table: "GameParticipants");
        }
    }
}
