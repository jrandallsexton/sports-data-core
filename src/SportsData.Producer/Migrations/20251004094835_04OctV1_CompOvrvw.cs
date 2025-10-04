using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Producer.Migrations
{
    /// <inheritdoc />
    public partial class _04OctV1_CompOvrvw : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CompetitionCompetitorStatistics_Competition_CompetitionId",
                table: "CompetitionCompetitorStatistics");

            migrationBuilder.DropForeignKey(
                name: "FK_CompetitionCompetitorStatistics_FranchiseSeason_FranchiseSe~",
                table: "CompetitionCompetitorStatistics");

            migrationBuilder.AddColumn<Guid>(
                name: "CompetitionCompetitorId",
                table: "CompetitionCompetitorStatistics",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionCompetitorStatistics_CompetitionCompetitorId",
                table: "CompetitionCompetitorStatistics",
                column: "CompetitionCompetitorId");

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionCompetitorStatistics_FranchiseSeasonId",
                table: "CompetitionCompetitorStatistics",
                column: "FranchiseSeasonId");

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionCompetitor_CompetitionId_HomeAway",
                table: "CompetitionCompetitor",
                columns: new[] { "CompetitionId", "HomeAway" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionCompetitor_CompetitionId_Order",
                table: "CompetitionCompetitor",
                columns: new[] { "CompetitionId", "Order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionCompetitor_FranchiseSeasonId",
                table: "CompetitionCompetitor",
                column: "FranchiseSeasonId");

            migrationBuilder.AddForeignKey(
                name: "FK_CompetitionCompetitor_FranchiseSeason_FranchiseSeasonId",
                table: "CompetitionCompetitor",
                column: "FranchiseSeasonId",
                principalTable: "FranchiseSeason",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CompetitionCompetitorStatistics_CompetitionCompetitor_Compe~",
                table: "CompetitionCompetitorStatistics",
                column: "CompetitionCompetitorId",
                principalTable: "CompetitionCompetitor",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_CompetitionCompetitorStatistics_Competition_CompetitionId",
                table: "CompetitionCompetitorStatistics",
                column: "CompetitionId",
                principalTable: "Competition",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CompetitionCompetitorStatistics_FranchiseSeason_FranchiseSe~",
                table: "CompetitionCompetitorStatistics",
                column: "FranchiseSeasonId",
                principalTable: "FranchiseSeason",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CompetitionPrediction_Competition_CompetitionId",
                table: "CompetitionPrediction",
                column: "CompetitionId",
                principalTable: "Competition",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CompetitionCompetitor_FranchiseSeason_FranchiseSeasonId",
                table: "CompetitionCompetitor");

            migrationBuilder.DropForeignKey(
                name: "FK_CompetitionCompetitorStatistics_CompetitionCompetitor_Compe~",
                table: "CompetitionCompetitorStatistics");

            migrationBuilder.DropForeignKey(
                name: "FK_CompetitionCompetitorStatistics_Competition_CompetitionId",
                table: "CompetitionCompetitorStatistics");

            migrationBuilder.DropForeignKey(
                name: "FK_CompetitionCompetitorStatistics_FranchiseSeason_FranchiseSe~",
                table: "CompetitionCompetitorStatistics");

            migrationBuilder.DropForeignKey(
                name: "FK_CompetitionPrediction_Competition_CompetitionId",
                table: "CompetitionPrediction");

            migrationBuilder.DropIndex(
                name: "IX_CompetitionCompetitorStatistics_CompetitionCompetitorId",
                table: "CompetitionCompetitorStatistics");

            migrationBuilder.DropIndex(
                name: "IX_CompetitionCompetitorStatistics_FranchiseSeasonId",
                table: "CompetitionCompetitorStatistics");

            migrationBuilder.DropIndex(
                name: "IX_CompetitionCompetitor_CompetitionId_HomeAway",
                table: "CompetitionCompetitor");

            migrationBuilder.DropIndex(
                name: "IX_CompetitionCompetitor_CompetitionId_Order",
                table: "CompetitionCompetitor");

            migrationBuilder.DropIndex(
                name: "IX_CompetitionCompetitor_FranchiseSeasonId",
                table: "CompetitionCompetitor");

            migrationBuilder.DropColumn(
                name: "CompetitionCompetitorId",
                table: "CompetitionCompetitorStatistics");

            migrationBuilder.AddForeignKey(
                name: "FK_CompetitionCompetitorStatistics_Competition_CompetitionId",
                table: "CompetitionCompetitorStatistics",
                column: "CompetitionId",
                principalTable: "Competition",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_CompetitionCompetitorStatistics_FranchiseSeason_FranchiseSe~",
                table: "CompetitionCompetitorStatistics",
                column: "FranchiseSeasonId",
                principalTable: "FranchiseSeason",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
