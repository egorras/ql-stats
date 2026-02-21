using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QLStats.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveServerName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Name",
                table: "QLServers");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "QLServers",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");
        }
    }
}
