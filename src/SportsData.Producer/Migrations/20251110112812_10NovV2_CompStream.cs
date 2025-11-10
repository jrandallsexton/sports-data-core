using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Producer.Migrations
{
    /// <inheritdoc />
    public partial class _10NovV2_CompStream : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_CompetitionStream_SeasonWeekId",
                table: "CompetitionStream",
                column: "SeasonWeekId");

            migrationBuilder.AddForeignKey(
                name: "FK_CompetitionStream_SeasonWeek_SeasonWeekId",
                table: "CompetitionStream",
                column: "SeasonWeekId",
                principalTable: "SeasonWeek",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CompetitionStream_SeasonWeek_SeasonWeekId",
                table: "CompetitionStream");

            migrationBuilder.DropIndex(
                name: "IX_CompetitionStream_SeasonWeekId",
                table: "CompetitionStream");
        }
    }
}
