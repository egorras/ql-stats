using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QLStats.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDamageTaken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DamageTaken",
                table: "MatchPlayers",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DamageTaken",
                table: "MatchPlayers");
        }
    }
}
