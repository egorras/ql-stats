using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace QLStats.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSeasonStandings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "season_standings",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    season_id = table.Column<int>(type: "integer", nullable: false),
                    player_id = table.Column<int>(type: "integer", nullable: false),
                    total_points = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: false),
                    kills = table.Column<int>(type: "integer", nullable: false),
                    deaths = table.Column<int>(type: "integer", nullable: false),
                    wins = table.Column<int>(type: "integer", nullable: false),
                    losses = table.Column<int>(type: "integer", nullable: false),
                    rounds_won = table.Column<int>(type: "integer", nullable: false),
                    rounds_lost = table.Column<int>(type: "integer", nullable: false),
                    damage_dealt = table.Column<int>(type: "integer", nullable: false),
                    matches_played = table.Column<int>(type: "integer", nullable: false),
                    snapped_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_season_standings", x => x.id);
                    table.ForeignKey(
                        name: "fk_season_standings_players_player_id",
                        column: x => x.player_id,
                        principalTable: "players",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_season_standings_seasons_season_id",
                        column: x => x.season_id,
                        principalTable: "seasons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_season_standings_player_id",
                table: "season_standings",
                column: "player_id");

            migrationBuilder.CreateIndex(
                name: "ix_season_standings_season_id_player_id",
                table: "season_standings",
                columns: new[] { "season_id", "player_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "season_standings");
        }
    }
}
