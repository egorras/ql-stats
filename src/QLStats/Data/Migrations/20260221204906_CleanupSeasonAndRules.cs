using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QLStats.Data.Migrations
{
    /// <inheritdoc />
    public partial class CleanupSeasonAndRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PointsPerDamageDealt",
                table: "Seasons");

            migrationBuilder.DropColumn(
                name: "PointsPerDeath",
                table: "Seasons");

            migrationBuilder.DropColumn(
                name: "PointsPerKill",
                table: "Seasons");

            migrationBuilder.DropColumn(
                name: "PointsPerLoss",
                table: "Seasons");

            migrationBuilder.DropColumn(
                name: "PointsPerRoundLost",
                table: "Seasons");

            migrationBuilder.DropColumn(
                name: "PointsPerRoundWon",
                table: "Seasons");

            migrationBuilder.DropColumn(
                name: "PointsPerWin",
                table: "Seasons");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "ScoringRules");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "PointsPerDamageDealt",
                table: "Seasons",
                type: "numeric(10,6)",
                precision: 10,
                scale: 6,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PointsPerDeath",
                table: "Seasons",
                type: "numeric(10,4)",
                precision: 10,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PointsPerKill",
                table: "Seasons",
                type: "numeric(10,4)",
                precision: 10,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PointsPerLoss",
                table: "Seasons",
                type: "numeric(10,4)",
                precision: 10,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PointsPerRoundLost",
                table: "Seasons",
                type: "numeric(10,4)",
                precision: 10,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PointsPerRoundWon",
                table: "Seasons",
                type: "numeric(10,4)",
                precision: 10,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PointsPerWin",
                table: "Seasons",
                type: "numeric(10,4)",
                precision: 10,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "ScoringRules",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");
        }
    }
}
