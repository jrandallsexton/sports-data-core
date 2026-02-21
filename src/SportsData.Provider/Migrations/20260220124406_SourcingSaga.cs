using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Provider.Migrations
{
    /// <inheritdoc />
    public partial class SourcingSaga : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HistoricalSourcingSagas",
                columns: table => new
                {
                    CorrelationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CurrentState = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Sport = table.Column<int>(type: "integer", nullable: false),
                    SeasonYear = table.Column<int>(type: "integer", nullable: false),
                    Provider = table.Column<int>(type: "integer", nullable: false),
                    SeasonCompletionEventsReceived = table.Column<int>(type: "integer", nullable: false),
                    VenueCompletionEventsReceived = table.Column<int>(type: "integer", nullable: false),
                    TeamSeasonCompletionEventsReceived = table.Column<int>(type: "integer", nullable: false),
                    AthleteSeasonCompletionEventsReceived = table.Column<int>(type: "integer", nullable: false),
                    StartedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SeasonCompletedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    VenueCompletedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TeamSeasonCompletedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AthleteSeasonCompletedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistoricalSourcingSagas", x => x.CorrelationId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HistoricalSourcingSagas_CurrentState",
                table: "HistoricalSourcingSagas",
                column: "CurrentState");

            migrationBuilder.CreateIndex(
                name: "IX_HistoricalSourcingSagas_Sport_Season",
                table: "HistoricalSourcingSagas",
                columns: new[] { "Sport", "SeasonYear" });

            migrationBuilder.CreateIndex(
                name: "IX_HistoricalSourcingSagas_StartedUtc",
                table: "HistoricalSourcingSagas",
                column: "StartedUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HistoricalSourcingSagas");
        }
    }
}
