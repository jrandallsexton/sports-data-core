using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Producer.Migrations
{
    /// <inheritdoc />
    public partial class _24DecV2_DoubleRel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AthleteCompetitionStatisticCategory_AthleteCompetitionStat~1",
                table: "AthleteCompetitionStatisticCategory");

            migrationBuilder.DropIndex(
                name: "IX_AthleteCompetitionStatisticCategory_AthleteCompetitionStat~1",
                table: "AthleteCompetitionStatisticCategory");

            migrationBuilder.DropColumn(
                name: "AthleteCompetitionStatisticId1",
                table: "AthleteCompetitionStatisticCategory");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AthleteCompetitionStatisticId1",
                table: "AthleteCompetitionStatisticCategory",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AthleteCompetitionStatisticCategory_AthleteCompetitionStat~1",
                table: "AthleteCompetitionStatisticCategory",
                column: "AthleteCompetitionStatisticId1");

            migrationBuilder.AddForeignKey(
                name: "FK_AthleteCompetitionStatisticCategory_AthleteCompetitionStat~1",
                table: "AthleteCompetitionStatisticCategory",
                column: "AthleteCompetitionStatisticId1",
                principalTable: "AthleteCompetitionStatistic",
                principalColumn: "Id");
        }
    }
}
