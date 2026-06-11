using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HexMaster.ThePrey.Games.Data.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddHunterDelayAnchor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "DelayAnchorLatitude",
                table: "GameParticipants",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "DelayAnchorLongitude",
                table: "GameParticipants",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "DelayPenaltyApplied",
                table: "GameParticipants",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DelayAnchorLatitude",
                table: "GameParticipants");

            migrationBuilder.DropColumn(
                name: "DelayAnchorLongitude",
                table: "GameParticipants");

            migrationBuilder.DropColumn(
                name: "DelayPenaltyApplied",
                table: "GameParticipants");
        }
    }
}
