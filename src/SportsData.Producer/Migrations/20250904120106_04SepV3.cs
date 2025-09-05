using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Producer.Migrations
{
    /// <inheritdoc />
    public partial class _04SepV3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SeasonPollWeekExternalId_SeasonPollWeek_WeekId",
                table: "SeasonPollWeekExternalId");

            migrationBuilder.DropIndex(
                name: "IX_SeasonPollWeekExternalId_WeekId",
                table: "SeasonPollWeekExternalId");

            migrationBuilder.DropColumn(
                name: "WeekId",
                table: "SeasonPollWeekExternalId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "WeekId",
                table: "SeasonPollWeekExternalId",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_SeasonPollWeekExternalId_WeekId",
                table: "SeasonPollWeekExternalId",
                column: "WeekId");

            migrationBuilder.AddForeignKey(
                name: "FK_SeasonPollWeekExternalId_SeasonPollWeek_WeekId",
                table: "SeasonPollWeekExternalId",
                column: "WeekId",
                principalTable: "SeasonPollWeek",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
