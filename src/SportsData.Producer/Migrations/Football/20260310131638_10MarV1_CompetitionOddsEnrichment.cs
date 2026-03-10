using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Producer.Migrations.Football
{
    /// <inheritdoc />
    public partial class _10MarV1_CompetitionOddsEnrichment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AtsWinnerFranchiseSeasonId",
                table: "CompetitionOdds",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EnrichedUtc",
                table: "CompetitionOdds",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OverUnderResult",
                table: "CompetitionOdds",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "WinnerFranchiseSeasonId",
                table: "CompetitionOdds",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionOdds_AtsWinnerFranchiseSeasonId",
                table: "CompetitionOdds",
                column: "AtsWinnerFranchiseSeasonId");

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionOdds_WinnerFranchiseSeasonId",
                table: "CompetitionOdds",
                column: "WinnerFranchiseSeasonId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CompetitionOdds_AtsWinnerFranchiseSeasonId",
                table: "CompetitionOdds");

            migrationBuilder.DropIndex(
                name: "IX_CompetitionOdds_WinnerFranchiseSeasonId",
                table: "CompetitionOdds");

            migrationBuilder.DropColumn(
                name: "AtsWinnerFranchiseSeasonId",
                table: "CompetitionOdds");

            migrationBuilder.DropColumn(
                name: "EnrichedUtc",
                table: "CompetitionOdds");

            migrationBuilder.DropColumn(
                name: "OverUnderResult",
                table: "CompetitionOdds");

            migrationBuilder.DropColumn(
                name: "WinnerFranchiseSeasonId",
                table: "CompetitionOdds");
        }
    }
}
