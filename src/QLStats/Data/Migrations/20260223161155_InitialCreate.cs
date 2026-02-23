using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace QLStats.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "players",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    steam_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    display_name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    first_seen_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_seen_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_players", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ql_servers",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    zmq_address = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    reconnect_interval_ms = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ql_servers", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "seasons",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_seasons", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "matches",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ql_server_id = table.Column<int>(type: "integer", nullable: false),
                    match_guid = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    map = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    game_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    server_title = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    finished_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    team_red_rounds = table.Column<int>(type: "integer", nullable: true),
                    team_blue_rounds = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_matches", x => x.id);
                    table.ForeignKey(
                        name: "fk_matches_ql_servers_ql_server_id",
                        column: x => x.ql_server_id,
                        principalTable: "ql_servers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "zmq_events",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ql_server_id = table.Column<int>(type: "integer", nullable: false),
                    received_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    event_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    raw_json = table.Column<string>(type: "text", nullable: false),
                    processed = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_zmq_events", x => x.id);
                    table.ForeignKey(
                        name: "fk_zmq_events_ql_servers_ql_server_id",
                        column: x => x.ql_server_id,
                        principalTable: "ql_servers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "scoring_rules",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    season_id = table.Column<int>(type: "integer", nullable: false),
                    type = table.Column<int>(type: "integer", nullable: false),
                    value = table.Column<decimal>(type: "numeric(10,6)", precision: 10, scale: 6, nullable: false),
                    threshold = table.Column<decimal>(type: "numeric(10,6)", precision: 10, scale: 6, nullable: true),
                    game_type_filter = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    medal_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_scoring_rules", x => x.id);
                    table.ForeignKey(
                        name: "fk_scoring_rules_seasons_season_id",
                        column: x => x.season_id,
                        principalTable: "seasons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "match_players",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    match_id = table.Column<int>(type: "integer", nullable: false),
                    player_id = table.Column<int>(type: "integer", nullable: false),
                    team = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    won = table.Column<bool>(type: "boolean", nullable: false),
                    kills = table.Column<int>(type: "integer", nullable: false),
                    deaths = table.Column<int>(type: "integer", nullable: false),
                    suicides = table.Column<int>(type: "integer", nullable: false),
                    damage_dealt = table.Column<int>(type: "integer", nullable: false),
                    damage_taken = table.Column<int>(type: "integer", nullable: false),
                    rounds_won = table.Column<int>(type: "integer", nullable: false),
                    rounds_lost = table.Column<int>(type: "integer", nullable: false),
                    medals = table.Column<string>(type: "jsonb", nullable: false),
                    weapons = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_match_players", x => x.id);
                    table.ForeignKey(
                        name: "fk_match_players_matches_match_id",
                        column: x => x.match_id,
                        principalTable: "matches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_match_players_players_player_id",
                        column: x => x.player_id,
                        principalTable: "players",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "round_results",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    match_id = table.Column<int>(type: "integer", nullable: false),
                    round_number = table.Column<int>(type: "integer", nullable: false),
                    team_won = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_round_results", x => x.id);
                    table.ForeignKey(
                        name: "fk_round_results_matches_match_id",
                        column: x => x.match_id,
                        principalTable: "matches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_match_players_match_id_player_id",
                table: "match_players",
                columns: new[] { "match_id", "player_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_match_players_player_id",
                table: "match_players",
                column: "player_id");

            migrationBuilder.CreateIndex(
                name: "ix_matches_match_guid",
                table: "matches",
                column: "match_guid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_matches_ql_server_id",
                table: "matches",
                column: "ql_server_id");

            migrationBuilder.CreateIndex(
                name: "ix_matches_started_at",
                table: "matches",
                column: "started_at");

            migrationBuilder.CreateIndex(
                name: "ix_players_steam_id",
                table: "players",
                column: "steam_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_round_results_match_id_round_number",
                table: "round_results",
                columns: new[] { "match_id", "round_number" });

            migrationBuilder.CreateIndex(
                name: "ix_scoring_rules_season_id",
                table: "scoring_rules",
                column: "season_id");

            migrationBuilder.CreateIndex(
                name: "ix_seasons_one_active",
                table: "seasons",
                column: "is_active",
                unique: true,
                filter: "is_active = true");

            migrationBuilder.CreateIndex(
                name: "ix_zmq_events_processed",
                table: "zmq_events",
                column: "processed");

            migrationBuilder.CreateIndex(
                name: "ix_zmq_events_ql_server_id",
                table: "zmq_events",
                column: "ql_server_id");

            migrationBuilder.CreateIndex(
                name: "ix_zmq_events_received_at",
                table: "zmq_events",
                column: "received_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "match_players");

            migrationBuilder.DropTable(
                name: "round_results");

            migrationBuilder.DropTable(
                name: "scoring_rules");

            migrationBuilder.DropTable(
                name: "zmq_events");

            migrationBuilder.DropTable(
                name: "players");

            migrationBuilder.DropTable(
                name: "matches");

            migrationBuilder.DropTable(
                name: "seasons");

            migrationBuilder.DropTable(
                name: "ql_servers");
        }
    }
}
