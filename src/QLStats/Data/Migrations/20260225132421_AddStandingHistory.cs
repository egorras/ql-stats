using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QLStats.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStandingHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "current_rank",
                table: "season_standings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "last_rank",
                table: "season_standings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "last_total_points",
                table: "season_standings",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "current_rank",
                table: "season_standings");

            migrationBuilder.DropColumn(
                name: "last_rank",
                table: "season_standings");

            migrationBuilder.DropColumn(
                name: "last_total_points",
                table: "season_standings");
        }
    }
}
