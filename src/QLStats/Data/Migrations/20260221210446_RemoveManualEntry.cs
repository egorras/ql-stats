using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QLStats.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveManualEntry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsManualEntry",
                table: "Matches");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsManualEntry",
                table: "Matches",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}
