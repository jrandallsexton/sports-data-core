using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Producer.Migrations
{
    /// <inheritdoc />
    public partial class _30JanV1_TeamSeasonLeaders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FranchiseSeasonLeaderStat_FranchiseSeason_FranchiseSeasonId",
                table: "FranchiseSeasonLeaderStat");

            migrationBuilder.DropIndex(
                name: "IX_FranchiseSeasonLeaderStat_FranchiseSeasonId",
                table: "FranchiseSeasonLeaderStat");

            migrationBuilder.DropColumn(
                name: "FranchiseSeasonId",
                table: "FranchiseSeasonLeaderStat");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "FranchiseSeasonId",
                table: "FranchiseSeasonLeaderStat",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_FranchiseSeasonLeaderStat_FranchiseSeasonId",
                table: "FranchiseSeasonLeaderStat",
                column: "FranchiseSeasonId");

            migrationBuilder.AddForeignKey(
                name: "FK_FranchiseSeasonLeaderStat_FranchiseSeason_FranchiseSeasonId",
                table: "FranchiseSeasonLeaderStat",
                column: "FranchiseSeasonId",
                principalTable: "FranchiseSeason",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
