using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Producer.Migrations
{
    /// <inheritdoc />
    public partial class _01FebV3_AthleteCompetitionCompetitorFK : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AthleteCompetition_CompetitionId_AthleteSeasonId",
                table: "AthleteCompetition");

            migrationBuilder.AddColumn<Guid>(
                name: "CompetitionCompetitorId",
                table: "AthleteCompetition",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_AthleteCompetition_CompetitionCompetitorId",
                table: "AthleteCompetition",
                column: "CompetitionCompetitorId");

            migrationBuilder.CreateIndex(
                name: "IX_AthleteCompetition_CompetitionId_CompetitionCompetitorId_At~",
                table: "AthleteCompetition",
                columns: new[] { "CompetitionId", "CompetitionCompetitorId", "AthleteSeasonId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_AthleteCompetition_CompetitionCompetitor_CompetitionCompeti~",
                table: "AthleteCompetition",
                column: "CompetitionCompetitorId",
                principalTable: "CompetitionCompetitor",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AthleteCompetition_CompetitionCompetitor_CompetitionCompeti~",
                table: "AthleteCompetition");

            migrationBuilder.DropIndex(
                name: "IX_AthleteCompetition_CompetitionCompetitorId",
                table: "AthleteCompetition");

            migrationBuilder.DropIndex(
                name: "IX_AthleteCompetition_CompetitionId_CompetitionCompetitorId_At~",
                table: "AthleteCompetition");

            migrationBuilder.DropColumn(
                name: "CompetitionCompetitorId",
                table: "AthleteCompetition");

            migrationBuilder.CreateIndex(
                name: "IX_AthleteCompetition_CompetitionId_AthleteSeasonId",
                table: "AthleteCompetition",
                columns: new[] { "CompetitionId", "AthleteSeasonId" },
                unique: true);
        }
    }
}
