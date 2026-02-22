using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace QLStats.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveGameSession : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Matches_GameSessions_GameSessionId",
                table: "Matches");

            migrationBuilder.DropTable(
                name: "GameSessions");

            migrationBuilder.DropIndex(
                name: "IX_Matches_GameSessionId",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "GameSessionId",
                table: "Matches");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "GameSessionId",
                table: "Matches",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "GameSessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SeasonId = table.Column<int>(type: "integer", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    SessionDate = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GameSessions_Seasons_SeasonId",
                        column: x => x.SeasonId,
                        principalTable: "Seasons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Matches_GameSessionId",
                table: "Matches",
                column: "GameSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_GameSessions_SeasonId",
                table: "GameSessions",
                column: "SeasonId");

            migrationBuilder.CreateIndex(
                name: "IX_GameSessions_SessionDate",
                table: "GameSessions",
                column: "SessionDate");

            migrationBuilder.AddForeignKey(
                name: "FK_Matches_GameSessions_GameSessionId",
                table: "Matches",
                column: "GameSessionId",
                principalTable: "GameSessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
