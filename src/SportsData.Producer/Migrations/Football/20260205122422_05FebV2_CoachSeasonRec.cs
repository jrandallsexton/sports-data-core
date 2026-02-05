using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Producer.Migrations.Football
{
    /// <inheritdoc />
    public partial class _05FebV2_CoachSeasonRec : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CoachSeasonRecord_CoachSeason_CoachSeasonId1",
                table: "CoachSeasonRecord");

            migrationBuilder.DropIndex(
                name: "IX_CoachSeasonRecord_CoachSeasonId1",
                table: "CoachSeasonRecord");

            migrationBuilder.DropColumn(
                name: "CoachSeasonId1",
                table: "CoachSeasonRecord");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CoachSeasonId1",
                table: "CoachSeasonRecord",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CoachSeasonRecord_CoachSeasonId1",
                table: "CoachSeasonRecord",
                column: "CoachSeasonId1");

            migrationBuilder.AddForeignKey(
                name: "FK_CoachSeasonRecord_CoachSeason_CoachSeasonId1",
                table: "CoachSeasonRecord",
                column: "CoachSeasonId1",
                principalTable: "CoachSeason",
                principalColumn: "Id");
        }
    }
}
